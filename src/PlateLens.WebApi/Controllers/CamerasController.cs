using Microsoft.AspNetCore.Mvc;
using PlateLens.Domain.Entities;
using PlateLens.WebApi.Models;
using PlateLens.WebApi.Services;

namespace PlateLens.WebApi.Controllers;

/// <summary>Traduz operações HTTP de câmeras para os casos de uso do CameraService.</summary>
[ApiController, Route("api/cameras")]
public class CamerasController(CameraService cameras) : ControllerBase
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

    /// <summary>Atualiza o retângulo de interesse da câmera.</summary>
    [HttpPut("{id:guid}/region")]
    public async Task<IActionResult> Region(Guid id, UpdateGateRegionRequest request, CancellationToken ct) { await cameras.UpdateRegionAsync(id, request, ct); return NoContent(); }

    /// <summary>Remove uma câmera de rede.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await cameras.DeleteAsync(id, ct); return NoContent(); }
}
