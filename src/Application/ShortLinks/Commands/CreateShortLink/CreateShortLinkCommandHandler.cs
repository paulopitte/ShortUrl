using MediatR;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Application.ShortLinks.DTOs;
using ShortUrl.Domain.Entities;

namespace ShortUrl.Application.ShortLinks.Commands.CreateShortLink;

/// <summary>
/// Handler responsável por persistir um novo <see cref="ShortLink"/>, gerar seu código Base62 de 7 caracteres
/// de forma determinística via <see cref="IShortCodeGenerator"/> e retornar <see cref="ShortLinkCreatedDto"/>.
/// </summary>
public sealed class CreateShortLinkCommandHandler(
    IApplicationDbContext dbContext,
    IShortCodeGenerator codeGenerator) : IRequestHandler<CreateShortLinkCommand, ShortLinkCreatedDto>
{
    private const string DefaultBaseUrl = "https://sho.rt";

    public async Task<ShortLinkCreatedDto> Handle(
        CreateShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = request.ExpiresAtUtc.HasValue
            ? ShortLink.NormalizeExpirationDate(request.ExpiresAtUtc.Value)
            : ShortLink.CalculateDefaultExpiration(nowUtc);

        var entity = new ShortLink
        {
            Code = $"~{Guid.NewGuid():N}"[..7],
            TargetUrl = request.TargetUrl.Trim(),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc,
            AccessCount = 0L
        };

        dbContext.ShortLinks.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        entity.Code = codeGenerator.Encode(entity.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
            ? DefaultBaseUrl
            : request.BaseUrl.TrimEnd('/');

        return new ShortLinkCreatedDto(
            entity.Code,
            $"{baseUrl}/{entity.Code}",
            entity.TargetUrl,
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc);
    }
}

