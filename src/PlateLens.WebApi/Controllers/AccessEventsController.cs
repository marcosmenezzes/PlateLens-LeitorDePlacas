using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Models;
using PlateLens.WebApi.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlateLens.WebApi.Controllers;

/// <summary>Expõe o histórico da Portaria e sua atualização em tempo real.</summary>
[ApiController, Route("api/access-events")]
public class AccessEventsController(AppDbContext db, RealtimeService realtime) : ControllerBase
{
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Retorna os cem movimentos mais recentes com veículo e câmera.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await db.AccessEvents.AsNoTracking().Include(x => x.Vehicle).Include(x => x.Camera)
            .OrderByDescending(x => x.OccurredAt).Take(100).ToListAsync(ct);
        return Ok(items.Select(x => AccessEventResponseFactory.Create(x, x.Vehicle, x.Camera)));
    }

    /// <summary>Mantém uma conexão SSE aberta e envia novos movimentos assim que são gravados.</summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        var subscription = realtime.Subscribe();
        try
        {
            await Response.WriteAsync(": connected\n\n", ct);
            await Response.Body.FlushAsync(ct);
            await foreach (var item in subscription.Reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(item, StreamJson)}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { realtime.Unsubscribe(subscription.Id); }
    }
}
