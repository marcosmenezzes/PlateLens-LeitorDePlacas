using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Domain.Rules;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Configuration;
using PlateLens.WebApi.Services;

namespace PlateLens.WebApi.Controllers;

/// <summary>Orquestra upload de quadros, inferência local e persistência do resultado.</summary>
[ApiController, Route("api/vision")]
public class VisionController(IConfiguration configuration, IWebHostEnvironment environment, IHttpClientFactory clients,
    AppDbContext db, AccessService access, CameraHeartbeatService heartbeats) : ControllerBase
{
    private static readonly SemaphoreSlim RecognitionQueue = new(1, 1);

    /// <summary>Informa se detector, classificador e OCR estão disponíveis no computador.</summary>
    [HttpGet("model")]
    public IActionResult ModelStatus()
    {
        var detector = ModelPath("ModelPath", "../../models/plate.pt");
        var classifier = ModelPath("ClassifierPath", "../../models/plate-type.pt");
        var ocr = ModelPath("OcrPath", "../../vision/apple-ocr");
        return Ok(new
        {
            available = System.IO.File.Exists(detector) && System.IO.File.Exists(classifier) && System.IO.File.Exists(ocr),
            detector = new { fileName = Path.GetFileName(detector), available = System.IO.File.Exists(detector) },
            classifier = new { fileName = Path.GetFileName(classifier), available = System.IO.File.Exists(classifier) },
            ocr = new { fileName = Path.GetFileName(ocr), available = System.IO.File.Exists(ocr) },
            minimumConfidence = configuration.GetValue("Vision:MinConfidence", .5)
        });
    }

    /// <summary>Valida um quadro, envia-o ao serviço Python e processa as placas devolvidas.</summary>
    [HttpPost("recognize"), RequestSizeLimit(10 * 1024 * 1024), EnableRateLimiting(DependencyInjection.VisionRateLimit)]
    public async Task<IActionResult> Recognize(IFormFile image, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        if (image.Length is <= 0 or > 10 * 1024 * 1024 || image.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("Envie uma imagem JPEG, PNG ou WebP de até 10 MB.");

        var camera = await db.Cameras.FirstOrDefaultAsync(item => item.IsActive, cancellationToken)
            ?? await db.Cameras.FindAsync([AppDbContext.NativeCameraId], cancellationToken)
            ?? throw new InvalidOperationException("Nenhuma câmera ativa foi encontrada.");
        var region = GateRegion.Normalize(camera.RegionX, camera.RegionY, camera.RegionWidth, camera.RegionHeight);
        heartbeats.Touch(camera.Id);

        await RecognitionQueue.WaitAsync(cancellationToken);
        try
        {

            using var content = new MultipartFormDataContent();
            var stream = new StreamContent(image.OpenReadStream());
            stream.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);
            content.Add(stream, "image", image.ContentType == "image/png" ? "frame.png" : image.ContentType == "image/webp" ? "frame.webp" : "frame.jpg");
            content.Add(new StringContent(region.X.ToString(CultureInfo.InvariantCulture)), "x");
            content.Add(new StringContent(region.Y.ToString(CultureInfo.InvariantCulture)), "y");
            content.Add(new StringContent(region.Width.ToString(CultureInfo.InvariantCulture)), "width");
            content.Add(new StringContent(region.Height.ToString(CultureInfo.InvariantCulture)), "height");

            VisionResponse? response;
            try
            {
                var serviceResponse = await clients.CreateClient("Vision").PostAsync("/recognize", content, cancellationToken);
                if (!serviceResponse.IsSuccessStatusCode) return StatusCode(503, new { message = "Serviço de visão indisponível ou modelos ainda em treinamento." });
                response = await serviceResponse.Content.ReadFromJsonAsync<VisionResponse>(cancellationToken);
            }
            catch (HttpRequestException)
            {
                return StatusCode(503, new { message = "Inicie o serviço de visão na porta 8001." });
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(503, new { message = "O serviço de visão demorou demais para responder." });
            }

            var observations = (response?.Detections ?? []).Select(detection => new PlateObservation(
                detection.Box, detection.Confidence, detection.RawText, detection.OcrConfidence,
                detection.PlateType, detection.TypeConfidence, detection.QualityScore)).ToArray();
            var processingMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            var result = await access.ProcessFrameAsync(camera, region, observations,
                configuration.GetValue("Vision:MinConfidence", .5), processingMs, cancellationToken);
            return Ok(new { result.Detections, result.Recorded, result.VehicleName, processingMs });
        }
        finally { RecognitionQueue.Release(); }
    }

    /// <summary>Resolve um caminho de modelo em relação à pasta do projeto da API.</summary>
    private string ModelPath(string key, string fallback) =>
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuration[$"Vision:{key}"] ?? fallback));

    public sealed record VisionResponse(VisionDetection[] Detections);
    public sealed record VisionDetection(NormalizedBox Box, double Confidence, string RawText, double OcrConfidence, string PlateType, double TypeConfidence, double QualityScore);
}
