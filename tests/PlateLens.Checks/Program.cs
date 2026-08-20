using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Domain.Rules;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Services;

var region = new GateRegion(.2, .25, .6, .5);
if (!region.IsValid || !region.Contains(.5, .5) || region.Contains(.1, .5)) throw new Exception("GateRegion falhou.");
if (!new GateRegion(.3, .2, .7000000000000001, .8).IsValid || new GateRegion(.5, .2, .6, .8).IsValid) throw new Exception("GateRegion não tratou arredondamento.");
var repaired = GateRegion.Normalize(2, double.NaN, .8, 4);
if (!repaired.IsValid || repaired.X != .99 || repaired.Y != .25 || repaired.Width != .01 || repaired.Height != .75) throw new Exception("GateRegion não corrigiu região inválida.");
if (!CameraNetworkPolicy.TryNormalizePrivateIpv4("192.168.1.20", out _) || CameraNetworkPolicy.TryNormalizePrivateIpv4("8.8.8.8", out _)) throw new Exception("CameraNetworkPolicy falhou.");
if (!PlateNumberRule.TryNormalize("abc-1234", out var oldPlate) || oldPlate != "ABC1234") throw new Exception("Placa antiga falhou.");
if (!PlateNumberRule.TryNormalize("abc1d23", out var mercosulPlate) || mercosulPlate != "ABC1D23") throw new Exception("Placa Mercosul falhou.");
if (PlateNumberRule.TryNormalize("ABC@123", out _)) throw new Exception("Placa inválida foi aceita.");
if (!PlateCaptureRule.ShouldCapture(region, new NormalizedBox(.4, .4, .2, .1), "ABC1D23", .92, .7, out _) || PlateCaptureRule.ShouldCapture(region, new NormalizedBox(0, 0, .1, .1), "ABC1D23", .92, .7, out _)) throw new Exception("PlateCaptureRule falhou.");
if (!PlateCaptureRule.ShouldCapture(region, new NormalizedBox(.1, .3, .2, .1), "ABC1D23", .92, .7, out _)) throw new Exception("Interseção parcial da placa falhou.");
if (!PlateCaptureRule.ShouldCapture(region, new NormalizedBox(.4, .4, .2, .1), "DQE2H66", .01, 0, out _)) throw new Exception("Placa brasileira confirmada pelo OCR foi rejeitada.");
var now = DateTime.UtcNow;
var tracker = new GateCrossingTracker(TimeSpan.Zero);
var firstObservation = tracker.Observe(Guid.Empty, "ABC1D23", .35, region, now);
if (!firstObservation.IsNewTrack || firstObservation.EventType is not null) throw new Exception("Tracking inicial falhou.");
if (tracker.Observe(Guid.Empty, "ABC1D23", .65, region, now.AddMilliseconds(600))?.EventType != PlateLens.Domain.Entities.AccessEventType.Entry) throw new Exception("Cruzamento de entrada falhou.");
if (tracker.Observe(Guid.Empty, "ABC1D23", .35, region, now.AddMilliseconds(1200))?.EventType != PlateLens.Domain.Entities.AccessEventType.Exit) throw new Exception("Cruzamento de saída falhou.");
var cooldownTracker = new GateCrossingTracker(TimeSpan.FromSeconds(5));
cooldownTracker.Observe(Guid.Empty, "ABC1D23", .35, region, now);
if (cooldownTracker.Observe(Guid.Empty, "ABC1D23", .65, region, now.AddSeconds(1)).EventType is null || cooldownTracker.Observe(Guid.Empty, "ABC1D23", .35, region, now.AddSeconds(2)).EventType is not null) throw new Exception("Cooldown falhou.");
var consensusCheck = new PlateConsensusTracker();
consensusCheck.Observe(Guid.Empty, "ABC1D23", .9, .9, now);
consensusCheck.Observe(Guid.Empty, "ABC1D28", .7, .5, now.AddMilliseconds(500));
if (consensusCheck.Observe(Guid.Empty, "ABC1D23", .9, .9, now.AddSeconds(1)) != "ABC1D23") throw new Exception("Consenso entre quadros falhou.");
var exactConsensus = new PlateConsensusTracker();
exactConsensus.Observe(Guid.Empty, "ABC1D23", .9, .9, now);
exactConsensus.Forget("ABC1D23");
if (exactConsensus.Observe(Guid.Empty, "ABC1D23", .9, .9, now.AddMilliseconds(250)) is not null || exactConsensus.Observe(Guid.Empty, "ABC1D23", .9, .9, now.AddMilliseconds(500)) != "ABC1D23")
    throw new Exception("Consenso exato ou limpeza da placa falhou.");

var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options;
await using var db = new AppDbContext(options);
await db.Database.OpenConnectionAsync();
await db.Database.EnsureCreatedAsync();
var camera = await db.Cameras.SingleAsync();
var gateTracker = new GateCrossingTracker(maxGap: TimeSpan.Zero);
var plateConsensus = new PlateConsensusTracker();
var access = new AccessService(db, gateTracker, plateConsensus, new RealtimeService());
async Task<FrameProcessingResult> ReadPlate()
{
    FrameProcessingResult? result = null;
    for (var index = 0; index < 2; index++)
        result = await access.ProcessFrameAsync(camera, region,
            [new PlateObservation(new NormalizedBox(.4, .35, .2, .1), .9, "ABC1D23", .9, "MERCOSUL", .9, .9)], .35, 100, default);
    return result!;
}
var processed = await ReadPlate();
var vehicleId = await db.Vehicles.Where(x => x.Plate == "ABC1D23").Select(x => x.Id).SingleAsync();
if (!processed.Recorded || processed.Detections.Single().Crossing != AccessEventType.Entry || await db.AccessEvents.CountAsync() != 1)
    throw new Exception("Primeira detecção não criou veículo e entrada juntos.");
processed = await access.ProcessFrameAsync(camera, region,
    [new PlateObservation(new NormalizedBox(.4, .35, .2, .1), .9, "ABC1D23", .9, "MERCOSUL", .9, .9)], .35, 100, default);
if (!processed.Recorded || processed.Detections.Single().Crossing != AccessEventType.Exit || await db.AccessEvents.CountAsync() != 2)
    throw new Exception("Placa conhecida não registrou saída na primeira leitura boa.");
await new VehicleService(db, gateTracker, plateConsensus).DeleteAsync(vehicleId, default);
if (await db.Vehicles.AnyAsync() || await db.AccessEvents.AnyAsync() || await db.RecognitionAttempts.AnyAsync())
    throw new Exception("Exclusão do veículo não removeu seus registros.");
Console.WriteLine("Checks passed.");
