namespace ShortUrl.Application.ShortLinks.DTOs;

/// <summary>
/// Dados do link curto recém-criado retornados pela API.
/// </summary>
/// <param name="Code">Código alfanumérico único de 7 caracteres em Base62.</param>
/// <param name="ShortUrl">URL curta completa pronta para compartilhamento.</param>
/// <param name="TargetUrl">URL original de destino.</param>
/// <param name="CreatedAtUtc">Data e hora exata de criação em UTC.</param>
/// <param name="ExpiresAtUtc">Instante limite de validade em UTC.</param>
public sealed record ShortLinkCreatedDto(
    string Code,
    string ShortUrl,
    string TargetUrl,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

