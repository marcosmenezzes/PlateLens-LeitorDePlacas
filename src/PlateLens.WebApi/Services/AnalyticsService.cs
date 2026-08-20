using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Models;

namespace PlateLens.WebApi.Services;

/// <summary>Calcula os indicadores operacionais usados pelo dashboard e pela página de estatísticas.</summary>
public sealed class AnalyticsService(AppDbContext db, CameraHeartbeatService heartbeats)
{
    private static readonly TimeZoneInfo LocalZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>Consulta um período local e produz séries, totais, permanência e métricas de reconhecimento.</summary>
    public async Task<AnalyticsResponse> GetAsync(DateOnly? requestedFrom, DateOnly? requestedTo, int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalZone));
        var to = requestedTo ?? today;
        var from = requestedFrom ?? to.AddDays(-(Math.Clamp(days, 1, 366) - 1));
        if (from > to || to.DayNumber - from.DayNumber > 365) throw new ArgumentException("Informe um período válido de até 366 dias.");
        var utcFrom = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), LocalZone);
        var utcTo = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), LocalZone);

        var events = await db.AccessEvents.AsNoTracking().Include(x => x.Vehicle).Include(x => x.Camera)
            .Where(x => x.OccurredAt >= utcFrom && x.OccurredAt < utcTo).OrderBy(x => x.OccurredAt).ToListAsync(ct);
        var allStates = await db.AccessEvents.AsNoTracking().Select(x => new { x.VehicleId, x.EventType, x.OccurredAt }).ToListAsync(ct);
        var inside = allStates.GroupBy(x => x.VehicleId).Count(group => group.MaxBy(x => x.OccurredAt)!.EventType == AccessEventType.Entry);
        var attempts = await db.RecognitionAttempts.AsNoTracking().Where(x => x.OccurredAt >= utcFrom && x.OccurredAt < utcTo).ToListAsync(ct);
        var cameras = await db.Cameras.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

        DateTime Local(DateTime value) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), LocalZone);
        var daily = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(offset =>
        {
            var date = from.AddDays(offset);
            var items = events.Where(x => DateOnly.FromDateTime(Local(x.OccurredAt)) == date).ToArray();
            return new TimelinePoint(date.ToString("dd/MM"), items.Count(x => x.EventType == AccessEventType.Entry), items.Count(x => x.EventType == AccessEventType.Exit), items.Length);
        }).ToArray();
        var hourly = Enumerable.Range(0, 24).Select(hour =>
        {
            var items = events.Where(x => Local(x.OccurredAt).Hour == hour).ToArray();
            return new TimelinePoint($"{hour:00}h", items.Count(x => x.EventType == AccessEventType.Entry), items.Count(x => x.EventType == AccessEventType.Exit), items.Length);
        }).ToArray();
        var vehicleTypes = Enum.GetValues<VehicleType>();
        var typeCounts = await db.Vehicles.AsNoTracking().GroupBy(x => x.VehicleType).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var byType = vehicleTypes.Select(type => new NamedCount(Display(type), typeCounts.FirstOrDefault(x => x.Key == type)?.Count ?? 0)).ToArray();
        var frequent = events.GroupBy(x => x.VehicleId).OrderByDescending(x => x.Count()).Take(10)
            .Select(x => new FrequentVehicle(x.First().Vehicle.Plate, x.First().Vehicle.Name, x.Count())).ToArray();
        var stays = PairStays(events).GroupBy(x => x.Type)
            .Select(x => new AverageStay(Display(x.Key), Math.Round(x.Average(y => y.Hours), 2))).ToArray();
        var valid = attempts.Count(x => x.FormatValid);
        var recognition = new RecognitionMetrics(attempts.Count, valid, attempts.Count - valid,
            attempts.Count == 0 ? 0 : Math.Round(valid * 100d / attempts.Count, 2),
            Average(attempts.Select(x => x.DetectionConfidence * 100)), Average(attempts.Select(x => x.OcrConfidence * 100)),
            Average(attempts.Select(x => x.ProcessingMs)), attempts.Count(x => !x.Accepted));
        var recent = events.OrderByDescending(x => x.OccurredAt).Take(10)
            .Select(x => AccessEventResponseFactory.Create(x, x.Vehicle, x.Camera)).ToArray();
        var peak = hourly.MaxBy(x => x.Count)!;

        return new(new(from, to), new(events.Count(x => x.EventType == AccessEventType.Entry),
            events.Count(x => x.EventType == AccessEventType.Exit), inside,
            events.Where(x => x.Vehicle.Name == "Desconhecido").Select(x => x.VehicleId).Distinct().Count(), events.Count),
            daily, hourly, byType, frequent, stays, recognition,
            cameras.Select(x => new CameraMetric(x.Id, x.Name, heartbeats.IsOnline(x.Id))).ToArray(), recent,
            peak.Count == 0 ? "--" : peak.Label);
    }

    /// <summary>Associa cada entrada à saída seguinte do mesmo veículo para medir permanência.</summary>
    private static IEnumerable<(VehicleType Type, double Hours)> PairStays(IEnumerable<AccessEvent> events)
    {
        foreach (var group in events.GroupBy(x => x.VehicleId))
        {
            DateTime? entry = null;
            foreach (var item in group.OrderBy(x => x.OccurredAt))
            {
                if (item.EventType == AccessEventType.Entry) entry = item.OccurredAt;
                else if (entry is not null && item.OccurredAt >= entry)
                {
                    yield return (item.Vehicle.VehicleType, (item.OccurredAt - entry.Value).TotalHours);
                    entry = null;
                }
            }
        }
    }

    /// <summary>Calcula uma média arredondada e devolve zero quando não há amostras.</summary>
    private static double Average(IEnumerable<double> values)
    {
        var items = values.ToArray();
        return items.Length == 0 ? 0 : Math.Round(items.Average(), 2);
    }

    /// <summary>Converte nomes internos de enumeração em rótulos apresentados na interface.</summary>
    private static string Display(VehicleType type) => type == VehicleType.Caminhao ? "Caminhão" : type.ToString();
}
