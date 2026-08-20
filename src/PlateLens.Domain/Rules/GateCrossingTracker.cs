using PlateLens.Domain.Entities;

namespace PlateLens.Domain.Rules;

public sealed record GateTracking(Guid TrackingId, AccessEventType? EventType, bool IsNewTrack);

/// <summary>Acompanha o lado da linha em que cada placa está para identificar entrada ou saída.</summary>
public sealed class GateCrossingTracker(TimeSpan? cooldown = null, TimeSpan? maxGap = null)
{
    private readonly TimeSpan _cooldown = cooldown ?? TimeSpan.FromSeconds(5);
    private readonly TimeSpan _maxGap = maxGap ?? TimeSpan.FromSeconds(2);
    private readonly Dictionary<(Guid CameraId, string Plate), TrackState> _tracks = [];
    private readonly object _lock = new();

    /// <summary>Registra a posição atual da placa e informa quando ela cruza a linha central.</summary>
    public GateTracking Observe(Guid cameraId, string plate, double centerY, GateRegion region, DateTime now)
    {
        var middle = region.Y + region.Height / 2;
        var deadband = Math.Max(.01, region.Height * .04);
        var side = centerY < middle - deadband ? -1 : centerY > middle + deadband ? 1 : 0;
        var key = (cameraId, plate);
        lock (_lock)
        {
            if (!_tracks.TryGetValue(key, out var state) || now - state.LastSeen > _maxGap)
            {
                var created = new TrackState(Guid.NewGuid(), side, now, DateTime.MinValue);
                _tracks[key] = created;
                return new(created.TrackingId, null, true);
            }
            state.LastSeen = now;
            if (side == 0 || state.Side == 0) { if (side != 0) state.Side = side; return new(state.TrackingId, null, false); }
            if (side == state.Side) return new(state.TrackingId, null, false);
            var eventType = state.Side < side ? AccessEventType.Entry : AccessEventType.Exit;
            state.Side = side;
            if (now - state.LastEvent < _cooldown) return new(state.TrackingId, null, false);
            state.LastEvent = now;
            return new GateTracking(state.TrackingId, eventType, false);
        }
    }

    /// <summary>Descarta o estado temporário de uma placa removida do cadastro.</summary>
    public void Forget(string plate)
    {
        lock (_lock)
            foreach (var key in _tracks.Keys.Where(key => key.Plate == plate).ToArray()) _tracks.Remove(key);
    }

    private sealed class TrackState(Guid trackingId, int side, DateTime lastSeen, DateTime lastEvent)
    {
        public Guid TrackingId { get; } = trackingId;
        public int Side { get; set; } = side;
        public DateTime LastSeen { get; set; } = lastSeen;
        public DateTime LastEvent { get; set; } = lastEvent;
    }
}
