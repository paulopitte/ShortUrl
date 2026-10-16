namespace ShortUrl.Domain.Exceptions;

/// <summary>
/// Exceção de domínio lançada quando um link curto existe, mas sua data de validade em UTC já expirou.
/// </summary>
public sealed class ShortLinkExpiredException : Exception
{
    public string? Code { get; }

    public ShortLinkExpiredException()
        : base("Este link curto expirou e não está mais disponível para redirecionamento.")
    {
    }

    public ShortLinkExpiredException(string code)
        : base("Este link curto expirou e não está mais disponível para redirecionamento.")
    {
        Code = code;
    }
}

