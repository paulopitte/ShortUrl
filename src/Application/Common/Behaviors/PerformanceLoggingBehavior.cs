using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace ShortUrl.Application.Common.Behaviors;

/// <summary>
/// Comportamento de pipeline do MediatR para medição de tempo de execução, enriquecimento de contexto
/// do Serilog e emissão de alertas de performance quando o limite configurado é ultrapassado.
/// </summary>
/// <typeparam name="TRequest">Tipo da requisição (Command ou Query).</typeparam>
/// <typeparam name="TResponse">Tipo da resposta.</typeparam>
public sealed class PerformanceLoggingBehavior<TRequest, TResponse>(
    ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long SlowRequestThresholdMilliseconds = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using (LogContext.PushProperty("MediatRRequestName", requestName))
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next(cancellationToken);
                stopwatch.Stop();

                var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                if (elapsedMilliseconds > SlowRequestThresholdMilliseconds)
                {
                    logger.LogWarning(
                        "Requisição MediatR de longa duração detectada: {RequestName} ({ElapsedMilliseconds} ms) {@Request}",
                        requestName,
                        elapsedMilliseconds,
                        request);
                }
                else
                {
                    logger.LogDebug(
                        "Requisição MediatR concluída: {RequestName} ({ElapsedMilliseconds} ms)",
                        requestName,
                        elapsedMilliseconds);
                }

                return response;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                throw;
            }
        }
    }
}

