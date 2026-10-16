using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using ShortUrl.Infrastructure.Configuration;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando ShortUrl Web Api...");

    var builder = WebApplication.CreateBuilder(args);

    // Configuração do Serilog como logging provider estruturado
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Configuração de opções (ShortUrlOptions) com suporte a appsettings, env vars e user-secrets
    builder.Services.Configure<ShortUrlOptions>(
        builder.Configuration.GetSection(ShortUrlOptions.SectionName));

    // Fundação de observabilidade com OpenTelemetry (Métricas e Tracing)
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing.AddSource("ShortUrl.Api");
        })
        .WithMetrics(metrics =>
        {
            metrics.AddMeter("ShortUrl.Api");
        });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.MapGet("/", () => Results.Ok(new
    {
        service = "ShortUrl API",
        version = "1.0.0",
        status = "Healthy"
    }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Falha fatal durante a inicialização da aplicação.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
