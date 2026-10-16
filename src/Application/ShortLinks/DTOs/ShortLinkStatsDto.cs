namespace ShortUrl.Application.ShortLinks.DTOs;

/// <summary>
/// Dados públicos de estatísticas e total de acessos acumulados de um link curto.
/// </summary>
/// <param name="Code">Código alfanumérico único de 7 caracteres em Base62.</param>
/// <param name="TargetUrl">URL original de destino.</param>
/// <param name="CreatedAtUtc">Data e hora exata de criação em UTC.</param>
/// <param name="ExpiresAtUtc">Instante limite de validade em UTC.</param>
/// <param name="IsExpired">Indica se o link já ultrapassou a sua data de expiração em UTC.</param>
/// <param name="AccessCount">Total acumulado de redirecionamentos válidos realizados.</param>
public sealed record ShortLinkStatsDto(
    string Code,
    string TargetUrl,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsExpired,
    long AccessCount);

