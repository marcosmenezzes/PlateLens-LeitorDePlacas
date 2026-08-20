using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Domain.Rules;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Models;

namespace PlateLens.WebApi.Services;

/// <summary>Executa os casos de uso de cadastro, edição e exclusão de veículos.</summary>
public sealed class VehicleService(AppDbContext db, GateCrossingTracker gateTracker, PlateConsensusTracker consensusTracker)
{
    /// <summary>Lista a frota e visitantes em ordem de placa sem manter rastreamento do EF.</summary>
    public Task<List<Vehicle>> ListAsync(CancellationToken ct) =>
        db.Vehicles.AsNoTracking().OrderBy(vehicle => vehicle.Plate).ToListAsync(ct);

    /// <summary>Cria um veículo ou atualiza o cadastro que já possui a mesma placa.</summary>
    public async Task<Vehicle> CreateAsync(UpsertVehicleRequest request, CancellationToken ct)
    {
        var plate = NormalizePlate(request.Plate);
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(item => item.Plate == plate, ct) ?? new Vehicle();
        Apply(vehicle, request, plate);
        if (vehicle.Id == Guid.Empty) db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);
        return vehicle;
    }

    /// <summary>Atualiza um veículo existente preservando seu histórico de acesso.</summary>
    public async Task UpdateAsync(Guid id, UpsertVehicleRequest request, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FindAsync([id], ct) ?? throw new KeyNotFoundException("Veículo não encontrado.");
        Apply(vehicle, request, NormalizePlate(request.Plate));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Remove o veículo, seu histórico persistido e qualquer estado temporário dos rastreadores.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FindAsync([id], ct) ?? throw new KeyNotFoundException("Veículo não encontrado.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.RecognitionAttempts.Where(attempt => attempt.NormalizedPlate == vehicle.Plate).ExecuteDeleteAsync(ct);
        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        gateTracker.Forget(vehicle.Plate);
        consensusTracker.Forget(vehicle.Plate);
    }

    /// <summary>Normaliza a placa e rejeita formatos que não seguem os padrões brasileiros suportados.</summary>
    private static string NormalizePlate(string value) =>
        PlateNumberRule.TryNormalize(value, out var plate) ? plate : throw new ArgumentException("Informe uma placa brasileira válida.");

    /// <summary>Copia somente os campos permitidos para a entidade, evitando atualização irrestrita.</summary>
    private static void Apply(Vehicle vehicle, UpsertVehicleRequest request, string plate)
    {
        vehicle.Plate = plate;
        vehicle.Name = string.IsNullOrWhiteSpace(request.Name) ? "Desconhecido" : request.Name.Trim();
        vehicle.VehicleType = request.VehicleType;
    }
}
