using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Application.ShortLinks.DTOs;
using ShortUrl.Domain.Exceptions;

namespace ShortUrl.Application.ShortLinks.Queries.GetShortLinkStats;

/// <summary>
/// Handler responsável por consultar de forma somente leitura (<c>AsNoTracking</c>) as estatísticas
/// e o total acumulado de acessos (<c>AccessCount</c>) de um link curto.
/// </summary>
/// <param name="dbContext">Contexto de banco de dados da aplicação para consulta somente leitura.</param>
public sealed class GetShortLinkStatsQueryHandler(
    IApplicationDbContext dbContext) : IRequestHandler<GetShortLinkStatsQuery, ShortLinkStatsDto>
{
    /// <summary>
    /// Processa a consulta somente leitura das estatísticas e acessos do link curto.
    /// </summary>
    /// <param name="request">Query contendo o código curto a ser consultado.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>DTO contendo as estatísticas e total acumulado de acessos do link.</returns>
    public async Task<ShortLinkStatsDto> Handle(
        GetShortLinkStatsQuery request,
        CancellationToken cancellationToken)
    {
        var shortLink = await dbContext.ShortLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == request.Code, cancellationToken);

        if (shortLink is null)
        {
            throw new ShortLinkNotFoundException(request.Code);
        }

        var nowUtc = DateTime.UtcNow;
        var isExpired = shortLink.IsExpired(nowUtc);

        return new ShortLinkStatsDto(
            shortLink.Code,
            shortLink.TargetUrl,
            shortLink.CreatedAtUtc,
            shortLink.ExpiresAtUtc,
            isExpired,
            shortLink.AccessCount);
    }
}

