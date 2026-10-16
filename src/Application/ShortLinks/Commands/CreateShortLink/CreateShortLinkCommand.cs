using MediatR;
using ShortUrl.Application.ShortLinks.DTOs;

namespace ShortUrl.Application.ShortLinks.Commands.CreateShortLink;

/// <summary>
/// Comando CQRS para criação de um novo link curto com código Base62 de 7 caracteres gerado automaticamente.
/// </summary>
/// <param name="TargetUrl">URL pública original de destino (http ou https, até 2048 caracteres).</param>
/// <param name="ExpiresAtUtc">Data opcional de expiração em UTC.</param>
/// <param name="BaseUrl">URL base pública para construção do link curto completo.</param>
public sealed record CreateShortLinkCommand(
    string TargetUrl,
    DateTime? ExpiresAtUtc = null,
    string? BaseUrl = null) : IRequest<ShortLinkCreatedDto>;

