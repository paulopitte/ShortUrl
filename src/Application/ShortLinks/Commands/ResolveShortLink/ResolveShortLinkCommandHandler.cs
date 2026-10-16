using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Domain.Exceptions;

namespace ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;

/// <summary>
/// Handler responsável por localizar um link curto pelo código e retornar sua URL de destino original.
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

        return shortLink.TargetUrl;
    }
}

