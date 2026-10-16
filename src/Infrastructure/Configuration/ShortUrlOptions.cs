namespace ShortUrl.Infrastructure.Configuration;

/// <summary>
/// Opções de configuração para o serviço ShortUrl.
/// Permite configuração via appsettings.json, variáveis de ambiente e user-secrets.
/// </summary>
public class ShortUrlOptions
{
    public const string SectionName = "ShortUrl";

    /// <summary>
    /// Sal secreto utilizado para embaralhar o alfabeto Base62 e ofuscar os códigos gerados.
    /// Não deve ser exposto publicamente nem versionado em texto plano no repositório.
    /// </summary>
    public string HashSalt { get; set; } = string.Empty;

    /// <summary>
    /// URL base pública opcional da aplicação para composição do link curto completo (ex: https://sho.rt).
    /// Caso omitido ou vazio, a URL base é inferida dinamicamente a partir do esquema e host da requisição HTTP atual.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Prazo padrão de expiração em anos quando nenhuma data for especificada na criação. Padrão: 10 anos.
    /// </summary>
    public int DefaultExpirationYears { get; set; } = 10;
}
