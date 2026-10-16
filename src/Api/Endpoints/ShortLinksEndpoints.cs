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

    /// <summary>
    /// Configura os endpoints pertencentes ao grupo de links sob <c>/shorturl-api/links</c>.
    /// </summary>
    /// <param name="group">Construtor do grupo de rotas da Minimal API.</param>
    public override void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateShortLinkAsync)
            .WithName("CreateShortLink")
            .WithSummary("Cria um novo link curto com código Base62 de 7 caracteres gerado automaticamente.")
            .WithDescription("Recebe uma URL original válida e pública e uma data de expiração opcional em UTC. Persiste o registro no SQLite e gera um código determinístico de 7 caracteres Base62 no cabeçalho Location e no corpo da resposta.")
            .Produces<ShortLinkCreatedDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{code}/stats", GetShortLinkStatsAsync)
            .WithName("GetShortLinkStats")
            .WithSummary("Consulta publicamente as estatísticas e o total de acessos acumulados de um link curto.")
            .WithDescription("Retorna as informações do link (URL de destino, data de criação, data de expiração, status de expiração e total de redirecionamentos válidos processados) para qualquer usuário que possua o código curto.")
            .Produces<ShortLinkStatsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Cria um novo link curto com código Base62 de 7 caracteres gerado automaticamente.
    /// </summary>
    /// <param name="request">Payload contendo a URL de destino e a data opcional de expiração em UTC.</param>
    /// <param name="sender">Instância do MediatR para despacho de comandos CQRS.</param>
    /// <param name="options">Configurações globais do serviço ShortUrl.</param>
    /// <param name="httpContext">Contexto HTTP da requisição atual.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Resultado HTTP 201 Created contendo os metadados do link curto criado e o cabeçalho Location.</returns>
    /// <response code="201">Link curto gerado com sucesso.</response>
    /// <response code="400">Dados inválidos fornecidos na requisição (URL não pública, comprimento excessivo ou data passada).</response>
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

    /// <summary>
    /// Consulta publicamente as estatísticas e o total de acessos acumulados de um link curto.
    /// </summary>
    /// <param name="code">Código alfanumérico Base62 de 7 caracteres do link curto.</param>
    /// <param name="sender">Instância do MediatR para despacho de queries CQRS.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Resultado HTTP 200 OK com os dados consolidados e contador de acessos do link.</returns>
    /// <response code="200">Estatísticas do link curto retornadas com sucesso.</response>
    /// <response code="400">Formato do código fornecido é inválido.</response>
    /// <response code="404">Link curto correspondente ao código não foi localizado no sistema.</response>
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

