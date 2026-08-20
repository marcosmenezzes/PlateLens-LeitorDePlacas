using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Rules;
using PlateLens.Infra.Data;
using PlateLens.WebApi.Services;

namespace PlateLens.WebApi.Configuration;

/// <summary>Registra infraestrutura, casos de uso e serviços compartilhados da aplicação.</summary>
public static class DependencyInjection
{
    public const string VisionRateLimit = "vision-recognition";

    /// <summary>Monta o grafo de dependências usado pela API sem executar trabalho de inicialização.</summary>
    public static IServiceCollection AddPlateLens(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
                await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Muitas capturas seguidas. Tentando novamente em alguns segundos." }, token);
            options.AddPolicy(VisionRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ =>
                    new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        services.AddCors(options => options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(configuration["Frontend:Origin"] ?? "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));
        services.AddScoped<CameraService>();
        services.AddScoped<AccessService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<VehicleService>();
        services.AddSingleton<GateCrossingTracker>();
        services.AddSingleton<PlateConsensusTracker>();
        services.AddSingleton<RealtimeService>();
        services.AddSingleton<CameraHeartbeatService>();
        services.AddHttpClient("Vision", client =>
        {
            client.BaseAddress = new Uri(configuration["Vision:ServiceUrl"] ?? "http://127.0.0.1:8001");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
