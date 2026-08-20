using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Domain.Rules;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Models;

namespace PlateLens.WebApi.Services;

public sealed record PlateObservation(NormalizedBox Box, double DetectionConfidence, string RawText,
    double OcrConfidence, string PlateType, double TypeConfidence, double QualityScore);

public sealed record ProcessedDetection(bool Detected, bool Accepted, bool PendingConsensus, bool FormatValid, bool InsideRegion,
    string Authenticity, string Plate, string PlateType, double Confidence, double DetectionConfidence,
    double OcrConfidence, double TypeConfidence, double QualityScore, NormalizedBox Box, Guid? TrackingId, AccessEventType? Crossing);

public sealed record FrameProcessingResult(ProcessedDetection[] Detections, bool Recorded, string? VehicleName);

/// <summary>Transforma observações do serviço de visão em veículos, tentativas e eventos de acesso.</summary>
public sealed class AccessService(AppDbContext db, GateCrossingTracker tracker, PlateConsensusTracker consensus, RealtimeService realtime)
{
    /// <summary>Valida todas as placas de um quadro e persiste os resultados em uma única unidade de trabalho.</summary>
    public async Task<FrameProcessingResult> ProcessFrameAsync(Camera camera, GateRegion region,
        IReadOnlyList<PlateObservation> observations, double minimumConfidence, double processingMs, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var detections = new List<ProcessedDetection>(observations.Count);
        var createdEvents = new List<(AccessEvent Event, Vehicle Vehicle)>();
        var vehicles = new Dictionary<string, Vehicle>();
        string? vehicleName = null;

        foreach (var observation in observations)
        {
            var confidence = Math.Sqrt(observation.DetectionConfidence * observation.OcrConfidence);
            var formatValid = PlateNumberRule.TryNormalize(observation.RawText, out var normalizedPlate);
            var insideRegion = region.Intersects(observation.Box);
            var eligible = formatValid && insideRegion && confidence >= minimumConfidence && observation.QualityScore >= .2;
            var knownVehicle = eligible ? await db.Vehicles.FirstOrDefaultAsync(x => x.Plate == normalizedPlate, ct) : null;
            var fastKnownPlate = knownVehicle is not null && confidence >= .5 && observation.QualityScore >= .4;
            var consensusPlate = fastKnownPlate ? normalizedPlate : eligible ? consensus.Observe(camera.Id, normalizedPlate, confidence, observation.QualityScore, now) : null;
            var accepted = consensusPlate is not null;
            var pendingConsensus = eligible && !accepted;
            if (accepted) normalizedPlate = consensusPlate!;
            GateTracking? tracking = null;
            Vehicle? vehicle = knownVehicle;
            var eventCreated = false;
            AccessEventType? confirmedEventType = null;

            if (accepted)
            {
                if (vehicle is null && !vehicles.TryGetValue(normalizedPlate, out vehicle))
                {
                    vehicle = await db.Vehicles.FirstOrDefaultAsync(x => x.Plate == normalizedPlate, ct)
                        ?? new Vehicle { Plate = normalizedPlate, Name = "Desconhecido", VehicleType = VehicleType.Desconhecido };
                    if (vehicle.Id == Guid.Empty) db.Vehicles.Add(vehicle);
                    vehicles[normalizedPlate] = vehicle;
                }
                vehicleName ??= vehicle.Name;
                tracking = tracker.Observe(camera.Id, normalizedPlate, observation.Box.CenterY, region, now);
                confirmedEventType = tracking.EventType;
                if (confirmedEventType is null && tracking.IsNewTrack)
                {
                    var previousEventType = await db.AccessEvents.Where(x => x.PlateDetected == normalizedPlate)
                        .OrderByDescending(x => x.OccurredAt).Select(x => (AccessEventType?)x.EventType).FirstOrDefaultAsync(ct);
                    confirmedEventType = previousEventType == AccessEventType.Entry ? AccessEventType.Exit : AccessEventType.Entry;
                }
                var hasRecentEvent = confirmedEventType is { } pendingEventType &&
                    (createdEvents.Any(x => x.Event.PlateDetected == normalizedPlate && x.Event.EventType == pendingEventType)
                    || await db.AccessEvents.AnyAsync(x => x.PlateDetected == normalizedPlate && x.EventType == pendingEventType && x.OccurredAt >= now.AddSeconds(-5), ct));
                if (confirmedEventType is { } eventType && !hasRecentEvent)
                {
                    var accessEvent = new AccessEvent
                    {
                        Vehicle = vehicle, Camera = camera, PlateDetected = normalizedPlate,
                        EventType = eventType, OccurredAt = now, Confidence = confidence * 100
                    };
                    db.AccessEvents.Add(accessEvent);
                    createdEvents.Add((accessEvent, vehicle));
                    eventCreated = true;
                }
            }

            var attempt = new RecognitionAttempt
            {
                Camera = camera, OccurredAt = now, RawText = observation.RawText,
                NormalizedPlate = formatValid ? normalizedPlate : null, PlateType = observation.PlateType,
                FormatValid = formatValid, InsideRegion = insideRegion, Accepted = accepted,
                EventCreated = eventCreated,
                DetectionConfidence = observation.DetectionConfidence, OcrConfidence = observation.OcrConfidence,
                ProcessingMs = processingMs, TrackingId = tracking?.TrackingId,
                RejectionReason = !formatValid ? "INVALID_FORMAT" : !insideRegion ? "OUTSIDE_REGION" : confidence < minimumConfidence ? "LOW_CONFIDENCE" : observation.QualityScore < .2 ? "LOW_QUALITY" : pendingConsensus ? "PENDING_CONSENSUS" : null
            };
            db.RecognitionAttempts.Add(attempt);
            detections.Add(new(true, accepted, pendingConsensus, formatValid, insideRegion, "UNVERIFIED",
                formatValid ? normalizedPlate : observation.RawText, observation.PlateType, confidence,
                observation.DetectionConfidence, observation.OcrConfidence, observation.TypeConfidence, observation.QualityScore,
                observation.Box, tracking?.TrackingId, eventCreated ? confirmedEventType : null));
        }

        await db.SaveChangesAsync(ct);
        foreach (var item in createdEvents) realtime.Publish(AccessEventResponseFactory.Create(item.Event, item.Vehicle, camera));
        return new(detections.ToArray(), createdEvents.Count > 0, vehicleName);
    }
}
