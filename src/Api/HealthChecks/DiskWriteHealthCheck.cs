using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShortUrl.Api.HealthChecks;

/// <summary>
/// Health check que valida a permissão e disponibilidade de escrita em disco para o banco SQLite e arquivos de log WAL.
/// </summary>
/// <remarks>
/// Executa a criação, gravação e exclusão de um arquivo de teste efêmero no diretório do banco de dados SQLite.
/// </remarks>
public sealed class DiskWriteHealthCheck(IConfiguration configuration) : IHealthCheck
{
    /// <summary>
    /// Executa a verificação de permissão e disponibilidade de escrita em disco.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Resultado da verificação de saúde com status e mensagem detalhada.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=shorturl.db";

            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;

            var targetDirectory = string.IsNullOrWhiteSpace(dataSource) || !Path.IsPathRooted(dataSource)
                ? AppContext.BaseDirectory
                : Path.GetDirectoryName(dataSource) ?? AppContext.BaseDirectory;

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var testFilePath = Path.Combine(targetDirectory, $".health_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFilePath, "probe");
            File.Delete(testFilePath);

            return Task.FromResult(HealthCheckResult.Healthy("Disponibilidade de escrita em disco verificada com sucesso."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Falha ao verificar escrita em disco: {ex.Message}", ex));
        }
    }
}

