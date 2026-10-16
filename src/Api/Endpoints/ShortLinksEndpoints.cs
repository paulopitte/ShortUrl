using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;
using ShortUrl.Application.ShortLinks.DTOs;
using ShortUrl.Application.ShortLinks.Queries.GetShortLinkStats;
using ShortUrl.Infrastructure.Configuration;

namespace ShortUrl.Api.Endpoints;

/// <summary>
/// Grupo de endpoints REST para criação e gerenciamento de links curtos sob <c>/shorturl-api/links</c>.
/// </summary>
public sealed class ShortLinksEndpoints : EndpointGroupBase
{
    public override string? GroupName => "links";

    public override void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateShortLinkAsync)
            .WithName("CreateShortLink")
            .WithSummary("Cria um novo link curto com código Base62 de 7 caracteres gerado automaticamente.")
            .Produces<ShortLinkCreatedDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{code}/stats", GetShortLinkStatsAsync)
            .WithName("GetShortLinkStats")
            .WithSummary("Consulta publicamente as estatísticas e o total de acessos acumulados de um link curto.")
            .Produces<ShortLinkStatsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateShortLinkAsync(
        CreateShortLinkRequest request,
        ISender sender,
        IOptions<ShortUrlOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var configuredBaseUrl = options.Value.BaseUrl;
        var baseUrl = !string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? configuredBaseUrl
            : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        var command = new CreateShortLinkCommand(
            request.TargetUrl ?? string.Empty,
            request.ExpiresAtUtc,
            baseUrl);

        var result = await sender.Send(command, cancellationToken);

        return Results.Created($"/{result.Code}", result);
    }

    private static async Task<IResult> GetShortLinkStatsAsync(
        string code,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetShortLinkStatsQuery(code), cancellationToken);
        return Results.Ok(result);
    }
}

/// <summary>
/// Payload de requisição para criação de um link curto.
/// </summary>
/// <param name="TargetUrl">URL pública original de destino (http ou https, até 2048 caracteres).</param>
/// <param name="ExpiresAtUtc">Data opcional de expiração em UTC.</param>
public sealed record CreateShortLinkRequest(
    string? TargetUrl,
    DateTime? ExpiresAtUtc = null);

