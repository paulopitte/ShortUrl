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

    /// <summary>
    /// Calcula a data de expiração padrão (10 anos após a data de criação em UTC).
    /// </summary>
    /// <param name="createdAtUtc">Instante de criação do link em UTC.</param>
    /// <returns>Instante limite de validade padrão em UTC.</returns>
    public static DateTime CalculateDefaultExpiration(DateTime createdAtUtc)
    {
        var utc = EnsureUtc(createdAtUtc);
        return utc.AddYears(10);
    }

    /// <summary>
    /// Normaliza uma data de expiração para o final do dia em UTC (23:59:59.9999999Z).
    /// </summary>
    /// <param name="dateTime">Data a ser normalizada.</param>
    /// <returns>Instante correspondente ao final do dia em UTC.</returns>
    public static DateTime NormalizeExpirationDate(DateTime dateTime)
    {
        var utc = EnsureUtc(dateTime);
        return new DateTime(utc.Year, utc.Month, utc.Day, 23, 59, 59, DateTimeKind.Utc)
            .AddTicks(9_999_999);
    }

    /// <summary>
    /// Verifica se o link já expirou em relação ao instante UTC fornecido.
    /// Um link é considerado ativo se <paramref name="nowUtc"/> for menor ou igual a <see cref="ExpiresAtUtc"/>.
    /// </summary>
    /// <param name="nowUtc">Instante atual em UTC.</param>
    /// <returns><c>true</c> se expirado; caso contrário, <c>false</c>.</returns>
    public bool IsExpired(DateTime nowUtc)
    {
        var utc = EnsureUtc(nowUtc);
        return utc > ExpiresAtUtc;
    }

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }
}

