using System.Collections.Concurrent;
using System.Threading.Channels;
using PlateLens.WebApi.Models;

namespace PlateLens.WebApi.Services;

/// <summary>Distribui eventos de acesso em memória para clientes conectados por SSE.</summary>
public sealed class RealtimeService
{
    private readonly ConcurrentDictionary<Guid, Channel<AccessEventResponse>> _subscribers = [];

    /// <summary>Cria uma assinatura limitada, descartando eventos antigos quando o cliente fica lento.</summary>
    public (Guid Id, ChannelReader<AccessEventResponse> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<AccessEventResponse>(new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    /// <summary>Encerra uma assinatura quando o cliente desconecta.</summary>
    public void Unsubscribe(Guid id) => _subscribers.TryRemove(id, out _);
    /// <summary>Publica um evento para todos os clientes atualmente conectados.</summary>
    public void Publish(AccessEventResponse item)
    {
        foreach (var channel in _subscribers.Values) channel.Writer.TryWrite(item);
    }
}
