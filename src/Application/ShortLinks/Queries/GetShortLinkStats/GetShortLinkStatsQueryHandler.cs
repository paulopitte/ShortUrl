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
public sealed class GetShortLinkStatsQueryHandler(
    IApplicationDbContext dbContext) : IRequestHandler<GetShortLinkStatsQuery, ShortLinkStatsDto>
{
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

