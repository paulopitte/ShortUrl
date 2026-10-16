using MediatR;
using ShortUrl.Application.ShortLinks.DTOs;

namespace ShortUrl.Application.ShortLinks.Queries.GetShortLinkStats;

/// <summary>
/// Query CQRS para consulta pública das estatísticas e total de acessos de um link curto pelo código.
/// </summary>
/// <param name="Code">Código alfanumérico Base62 de até 7 caracteres.</param>
public sealed record GetShortLinkStatsQuery(string Code) : IRequest<ShortLinkStatsDto>;

