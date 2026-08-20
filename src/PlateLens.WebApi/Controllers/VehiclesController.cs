using Microsoft.AspNetCore.Mvc;
using PlateLens.Domain.Entities;
using PlateLens.WebApi.Models;
using PlateLens.WebApi.Services;

namespace PlateLens.WebApi.Controllers;

/// <summary>Traduz operações HTTP de veículos para os casos de uso do VehicleService.</summary>
[ApiController, Route("api/vehicles")]
public class VehiclesController(VehicleService vehicles) : ControllerBase
{
    /// <summary>Retorna todos os veículos cadastrados.</summary>
    [HttpGet]
    public Task<List<Vehicle>> List(CancellationToken ct) => vehicles.ListAsync(ct);

    /// <summary>Cadastra um veículo informado manualmente.</summary>
    [HttpPost]
    public async Task<ActionResult<Vehicle>> Create(UpsertVehicleRequest request, CancellationToken ct)
    {
        return Ok(await vehicles.CreateAsync(request, ct));
    }

    /// <summary>Atualiza nome, placa e tipo de um veículo.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpsertVehicleRequest request, CancellationToken ct)
    {
        await vehicles.UpdateAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Exclui o veículo e todos os registros vinculados a ele.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await vehicles.DeleteAsync(id, ct);
        return NoContent();
    }
}
