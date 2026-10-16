namespace ShortUrl.Domain.Entities;

/// <summary>
/// Entidade raiz de agregado que representa um link curto no sistema.
/// </summary>
public class ShortLink
{
    /// <summary>
    /// Identificador numérico sequencial único utilizado como semente para geração do código Base62.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Código curto único de exatos 7 caracteres no alfabeto Base62 ([0-9A-Za-z]{7}).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// URL original de destino com esquema http ou https e host público válido (máximo 2048 caracteres).
    /// </summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora exata de criação do link em UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Instante limite de validade do link em UTC.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Contador inteiro monotônico crescente de redirecionamentos válidos realizados.
    /// </summary>
    public long AccessCount { get; set; }
}

