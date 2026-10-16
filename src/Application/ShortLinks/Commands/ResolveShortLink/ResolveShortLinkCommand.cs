using MediatR;

namespace ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;

/// <summary>
/// Comando CQRS para resolver um código curto e obter sua URL de destino original.
/// </summary>
/// <param name="Code">Código alfanumérico Base62 do link curto.</param>
public sealed record ResolveShortLinkCommand(string Code) : IRequest<string>;

