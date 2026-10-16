using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Infrastructure.Configuration;
using ShortUrl.Infrastructure.Data;

namespace ShortUrl.Api.IntegrationTests;

/// <summary>
/// Fábrica customizada de aplicação web para testes de integração, configurando um banco de dados
/// SQLite dedicado por instância de teste com modo WAL habilitado e esquema inicializado.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"shorturl_integration_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            var connectionString = $"Data Source={_databasePath};Cache=Shared;Mode=ReadWriteCreate;";

            services.Configure<ShortUrlOptions>(options =>
            {
                options.HashSalt = "integration-tests-secret-salt-2026";
                options.BaseUrl = "https://sho.rt";
                options.DefaultExpirationYears = 10;
            });

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connectionString);
                options.AddInterceptors(SqliteWalConnectionInterceptor.Instance);
            });

            services.AddScoped<IApplicationDbContext>(provider =>
                provider.GetRequiredService<ApplicationDbContext>());
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            TryDeleteFile(_databasePath);
            TryDeleteFile($"{_databasePath}-wal");
            TryDeleteFile($"{_databasePath}-shm");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignora falhas de limpeza de arquivos temporários em caso de lock transitório do sistema operacional.
        }
    }
}

