using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Domain.Exceptions;

namespace ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;

/// <summary>
/// Handler responsável por localizar um link curto pelo código, validar sua vigência, incrementar atomicamente
/// o contador de acessos (<c>AccessCount</c>) no banco de dados e retornar sua URL de destino original.
/// </summary>
public sealed class ResolveShortLinkCommandHandler(
    IApplicationDbContext dbContext) : IRequestHandler<ResolveShortLinkCommand, string>
{
    public async Task<string> Handle(
        ResolveShortLinkCommand request,
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
        if (nowUtc > shortLink.ExpiresAtUtc)
        {
            throw new ShortLinkExpiredException(request.Code);
        }

        var updatedRows = await dbContext.ShortLinks
            .Where(x => x.Id == shortLink.Id && x.ExpiresAtUtc >= nowUtc)
            .ExecuteUpdateAsync(
                s => s.SetProperty(b => b.AccessCount, b => b.AccessCount + 1),
                cancellationToken);

        if (updatedRows == 0)
        {
            throw new ShortLinkExpiredException(request.Code);
        }

        return shortLink.TargetUrl;
    }
}

