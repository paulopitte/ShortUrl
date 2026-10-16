namespace ShortUrl.Application.Common.Interfaces;

/// <summary>
/// Abstração para geração determinística e bijetiva de códigos curtos de 7 caracteres em Base62.
/// </summary>
public interface IShortCodeGenerator
{
    /// <summary>
    /// Codifica um identificador numérico sequencial positivo em um código Base62 ofuscado de exatos 7 caracteres.
    /// </summary>
    /// <param name="id">Identificador numérico sequencial positivo (1..62^7-1).</param>
    /// <returns>Código alfanumérico Base62 de 7 caracteres.</returns>
    string Encode(long id);

    /// <summary>
    /// Decodifica um código Base62 de 7 caracteres de volta para o identificador numérico original.
    /// </summary>
    /// <param name="code">Código alfanumérico Base62 de 7 caracteres.</param>
    /// <returns>Identificador numérico sequencial correspondente.</returns>
    long Decode(string code);
}

