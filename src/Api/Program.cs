using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using ShortUrl.Api.Endpoints;
using ShortUrl.Api.Middleware;
using ShortUrl.Application.Common.Behaviors;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Infrastructure.Configuration;
using ShortUrl.Infrastructure.Data;
using ShortUrl.Infrastructure.Services;

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
        .Enrich.FromLogContext(),
        preserveStaticLogger: true);

    // Configuração de opções (ShortUrlOptions) com suporte a appsettings, env vars e user-secrets
    builder.Services.Configure<ShortUrlOptions>(
        builder.Configuration.GetSection(ShortUrlOptions.SectionName));

    // Persistência SQLite via EF Core com modo WAL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=shorturl.db;Cache=Shared";

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlite(connectionString);
        options.AddInterceptors(SqliteWalConnectionInterceptor.Instance);
    });

    builder.Services.AddScoped<IApplicationDbContext>(provider =>
        provider.GetRequiredService<ApplicationDbContext>());

    // Serviço de geração/ofuscação Base62 de 7 caracteres
    builder.Services.AddSingleton<IShortCodeGenerator, Base62CodeGenerator>();

    // CQRS (MediatR), Pipeline Behaviors e Validadores FluentValidation
    var applicationAssembly = typeof(IApplicationDbContext).Assembly;
    builder.Services.AddValidatorsFromAssembly(applicationAssembly);
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(applicationAssembly);
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceLoggingBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    });

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

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    app.MapGet("/", () => Results.Ok(new
    {
        service = "ShortUrl API",
        version = "1.0.0",
        status = "Healthy"
    }));

    app.MapEndpointGroups();
    app.MapRedirectEndpoints();

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
