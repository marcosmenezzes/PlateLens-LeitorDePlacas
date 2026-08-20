using System.Collections.Concurrent;

namespace PlateLens.WebApi.Services;

/// <summary>Mantém em memória o último contato de cada câmera para indicar disponibilidade.</summary>
public sealed class CameraHeartbeatService
{
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = [];
    /// <summary>Atualiza o instante do último quadro recebido.</summary>
    public void Touch(Guid cameraId) => _lastSeen[cameraId] = DateTime.UtcNow;
    /// <summary>Considera online uma câmera vista nos últimos dez segundos.</summary>
    public bool IsOnline(Guid cameraId) => _lastSeen.TryGetValue(cameraId, out var seen) && seen >= DateTime.UtcNow.AddSeconds(-10);
}
