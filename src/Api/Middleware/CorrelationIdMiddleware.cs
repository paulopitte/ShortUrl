using System.Diagnostics;
using Serilog.Context;

namespace ShortUrl.Api.Middleware;

/// <summary>
/// Middleware responsável por extrair ou gerar o identificador de correlação (X-Correlation-Id),
/// propagá-lo nos cabeçalhos de resposta e enriquecer o contexto de log estruturado do Serilog.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string CorrelationIdLogPropertyName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdLogPropertyName] = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationIdLogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}

