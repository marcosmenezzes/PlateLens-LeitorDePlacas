using Microsoft.AspNetCore.Mvc;
using PlateLens.Domain.Entities;
using PlateLens.WebApi.Models;
using PlateLens.WebApi.Services;

namespace PlateLens.WebApi.Controllers;

/// <summary>Traduz operações HTTP de câmeras para os casos de uso do CameraService.</summary>
[ApiController, Route("api/cameras")]
public class CamerasController(CameraService cameras, IHttpClientFactory clients) : ControllerBase
{
    /// <summary>Lista as fontes de vídeo cadastradas.</summary>
    [HttpGet]
    public async Task<IReadOnlyCollection<Camera>> List(CancellationToken ct) => await cameras.ListAsync(ct);

    /// <summary>Cadastra e ativa uma câmera IPv4 da rede privada.</summary>
    [HttpPost]
    public async Task<ActionResult<Camera>> Register(RegisterCameraRequest request, CancellationToken ct)
    {
        var camera = await cameras.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(List), new { id = camera.Id }, camera);
    }
    /// <summary>Seleciona uma câmera como fonte ativa.</summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) { await cameras.ActivateAsync(id, ct); return NoContent(); }

    /// <summary>Encaminha o vídeo MJPEG da câmera IP ativa sem expor uma URL arbitrária.</summary>
    [HttpGet("{id:guid}/stream")]
    public async Task<IActionResult> Stream(Guid id, CancellationToken ct)
    {
        var camera = (await cameras.ListAsync(ct)).SingleOrDefault(item => item.Id == id && item.IsActive)
            ?? throw new KeyNotFoundException("Câmera não encontrada.");
        if (camera.SourceKind != CameraSourceKind.Network ||
            !Domain.Rules.CameraNetworkPolicy.TryNormalizePrivateIpv4(camera.IpAddress, out var ip) || camera.Port is not (>= 1 and <= 65535))
            throw new ArgumentException("A fonte selecionada não é uma câmera IP válida.");

        HttpResponseMessage upstream;
        try
        {
            upstream = await clients.CreateClient("NetworkCamera").GetAsync(
                new UriBuilder(Uri.UriSchemeHttp, ip, camera.Port.Value, "video").Uri,
                HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Não foi possível conectar à câmera IP." });
        }
        using var response = upstream;
        var contentType = response.Content.Headers.ContentType?.ToString();
        if (!response.IsSuccessStatusCode || contentType is null ||
            !(contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
              contentType.StartsWith("multipart/x-mixed-replace", StringComparison.OrdinalIgnoreCase)))
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "A câmera não forneceu um stream MJPEG em /video." });

        Response.ContentType = contentType;
        Response.Headers.CacheControl = "no-store";
        try { await response.Content.CopyToAsync(Response.Body, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        return new EmptyResult();
    }

    /// <summary>Atualiza o retângulo de interesse da câmera.</summary>
    [HttpPut("{id:guid}/region")]
    public async Task<IActionResult> Region(Guid id, UpdateGateRegionRequest request, CancellationToken ct) { await cameras.UpdateRegionAsync(id, request, ct); return NoContent(); }

    /// <summary>Remove uma câmera de rede.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await cameras.DeleteAsync(id, ct); return NoContent(); }
}
