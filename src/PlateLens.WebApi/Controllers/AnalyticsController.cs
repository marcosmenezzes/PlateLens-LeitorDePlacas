using Microsoft.AspNetCore.Mvc;
using PlateLens.WebApi.Services;

namespace PlateLens.WebApi.Controllers;

/// <summary>Disponibiliza indicadores consolidados do sistema.</summary>
[ApiController, Route("api/analytics")]
public class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>Retorna métricas do período solicitado ou dos últimos sete dias.</summary>
    [HttpGet]
    public Task<Models.AnalyticsResponse> Get(DateOnly? from, DateOnly? to, int days = 7, CancellationToken ct = default) =>
        analytics.GetAsync(from, to, days, ct);
}
