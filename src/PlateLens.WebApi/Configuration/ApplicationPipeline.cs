namespace PlateLens.WebApi.Configuration;

/// <summary>Define, em um único lugar, a ordem dos middlewares HTTP da API.</summary>
public static class ApplicationPipeline
{
    /// <summary>Configura tratamento de erros, CORS, limitação de uso e endpoints HTTP.</summary>
    public static WebApplication UsePlateLensPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseCors("Frontend");
        app.UseRateLimiter();
        app.MapControllers();
        return app;
    }
}
