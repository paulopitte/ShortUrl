namespace ShortUrl.Domain.Exceptions;

/// <summary>
/// Exceção de domínio lançada quando um código de link curto não é encontrado.
/// </summary>
public sealed class ShortLinkNotFoundException : Exception
{
    public string? Code { get; }

    public ShortLinkNotFoundException()
        : base("O link curto informado não foi encontrado.")
    {
    }

    public ShortLinkNotFoundException(string code)
        : base("O link curto informado não foi encontrado.")
    {
        Code = code;
    }
}

