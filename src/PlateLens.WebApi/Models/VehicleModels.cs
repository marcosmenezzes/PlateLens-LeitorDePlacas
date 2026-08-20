using PlateLens.Domain.Entities;

namespace PlateLens.WebApi.Models;

/// <summary>Dados aceitos ao cadastrar ou editar um veículo.</summary>
public sealed record UpsertVehicleRequest(string Plate, string Name, VehicleType VehicleType);
