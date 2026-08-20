using PlateLens.Domain.Entities;

namespace PlateLens.WebApi.Models;

public sealed record VehicleSummary(Guid Id, string Plate, string Name, VehicleType VehicleType);
public sealed record CameraSummary(Guid Id, string Name);
public sealed record AccessEventResponse(Guid Id, string PlateDetected, AccessEventType EventType, DateTime OccurredAt,
    double Confidence, VehicleSummary Vehicle, CameraSummary Camera);

public static class AccessEventResponseFactory
{
    public static AccessEventResponse Create(AccessEvent item, Vehicle vehicle, Camera camera) => new(
        item.Id, item.PlateDetected, item.EventType, DateTime.SpecifyKind(item.OccurredAt, DateTimeKind.Utc),
        Math.Round(item.Confidence, 2), new(vehicle.Id, vehicle.Plate, vehicle.Name, vehicle.VehicleType), new(camera.Id, camera.Name));
}
