namespace PlateLens.WebApi;

/// <summary>Converte exceções conhecidas em respostas HTTP consistentes sem expor detalhes internos.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Executa o próximo middleware e traduz falhas para status e mensagem apropriados.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            context.Response.StatusCode = exception switch
            {
                ArgumentException or InvalidOperationException => 400,
                KeyNotFoundException => 404,
                UnauthorizedAccessException => 401,
                _ => 500
            };
            if (context.Response.StatusCode >= 500)
                logger.LogError(exception, "Falha em {Method} {Path}. TraceId: {TraceId}", context.Request.Method, context.Request.Path, context.TraceIdentifier);
            else
                logger.LogWarning("Requisição rejeitada em {Method} {Path}: {Message}", context.Request.Method, context.Request.Path, exception.Message);
            await context.Response.WriteAsJsonAsync(new { status = context.Response.StatusCode, message = context.Response.StatusCode == 500 ? "Erro interno." : exception.Message });
        }
    }
}
