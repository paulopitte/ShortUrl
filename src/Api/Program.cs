using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using ShortUrl.Api.Endpoints;
using ShortUrl.Api.HealthChecks;
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

    // Configuração de Health Checks (SQLite e escrita em disco)
    builder.Services.AddHealthChecks()
        .AddCheck<SqliteDatabaseHealthCheck>("sqlite_db", tags: ["ready", "db"])
        .AddCheck<DiskWriteHealthCheck>("disk_write", tags: ["ready", "disk"]);

    // Documentação OpenAPI / NSwag Swagger UI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApiDocument(settings =>
    {
        settings.Title = "ShortUrl API";
        settings.Version = "v1";
        settings.Description = "Serviço REST de encurtamento de links de alta vazão com geração Base62 determinística, redirecionamento imediato e métricas públicas de acesso.";
        settings.DocumentName = "v1";
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

    // OpenAPI Specification e NSwag Swagger UI
    app.UseOpenApi();
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/swagger";
        settings.DocumentPath = "/swagger/v1/swagger.json";
    });

    app.MapGet("/", () => Results.Ok(new
    {
        service = "ShortUrl API",
        version = "1.0.0",
        status = "Healthy"
    })).WithName("RootStatus")
       .WithTags("System")
       .WithSummary("Informa a versão e status operacional básico da aplicação.");

    // Endpoints de Health Checks (Liveness e Readiness)
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = WriteHealthResponseAsync
    }).WithName("LivenessHealthCheck")
      .WithTags("Health");

    app.MapHealthChecks("/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    }).WithName("ReadinessHealthCheck")
      .WithTags("Health");

    app.MapEndpointGroups();
    app.MapRedirectEndpoints();

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    }

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

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var result = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            durationMs = e.Value.Duration.TotalMilliseconds,
            error = e.Value.Exception?.Message
        })
    };

    return context.Response.WriteAsJsonAsync(result);
}

public partial class Program { }
