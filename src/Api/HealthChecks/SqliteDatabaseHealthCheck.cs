using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShortUrl.Infrastructure.Data;

namespace ShortUrl.Api.HealthChecks;

/// <summary>
/// Health check que valida a conectividade e operabilidade do banco de dados SQLite.
/// </summary>
/// <remarks>
/// Executa um teste de conectividade e a consulta SQL leve <c>SELECT 1;</c> para assegurar que o banco está acessível.
/// </remarks>
public sealed class SqliteDatabaseHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    /// <summary>
    /// Executa a verificação de saúde da conexão com o banco de dados SQLite.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Resultado da verificação de saúde com status e mensagem detalhada.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados SQLite.");
            }

            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", cancellationToken);

            return HealthCheckResult.Healthy("Conexão com o banco de dados SQLite validada com sucesso.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Falha na comunicação com o banco de dados SQLite: {ex.Message}", ex);
        }
    }
}

