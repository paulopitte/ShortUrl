using MediatR;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Application.ShortLinks.DTOs;
using ShortUrl.Domain.Entities;

namespace ShortUrl.Application.ShortLinks.Commands.CreateShortLink;

/// <summary>
/// Handler responsável por persistir um novo <see cref="ShortLink"/>, gerar seu código Base62 de 7 caracteres
/// de forma determinística via <see cref="IShortCodeGenerator"/> e retornar <see cref="ShortLinkCreatedDto"/>.
/// </summary>
/// <param name="dbContext">Contexto de banco de dados da aplicação para persistência do link curto.</param>
/// <param name="codeGenerator">Gerador determinístico Base62 para ofuscação do identificador sequencial.</param>
public sealed class CreateShortLinkCommandHandler(
    IApplicationDbContext dbContext,
    IShortCodeGenerator codeGenerator) : IRequestHandler<CreateShortLinkCommand, ShortLinkCreatedDto>
{
    private const string DefaultBaseUrl = "https://sho.rt";

    /// <summary>
    /// Processa o comando de criação do link curto, gerando seu código único e calculando a data de expiração.
    /// </summary>
    /// <param name="request">Comando contendo a URL de destino e a data opcional de expiração em UTC.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação assíncrona.</param>
    /// <returns>DTO contendo os metadados do link criado.</returns>
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

