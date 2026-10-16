using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Infrastructure.Configuration;

namespace ShortUrl.Infrastructure.Services;

/// <summary>
/// Implementação de geração e decodificação bijetiva de códigos curtos de 7 caracteres em Base62
/// com permutação determinística por sal secreto (estilo Hashids) sobre identificadores de 64 bits.
/// </summary>
public sealed class Base62CodeGenerator : IShortCodeGenerator
{
    public const int CodeLength = 7;
    public const int Base = 62;

    /// <summary>
    /// Espaço combinatório total de 7 caracteres em Base62: 62^7 = 3.521.614.606.208.
    /// </summary>
    public const long MaxId = 3_521_614_606_207L; // 62^7 - 1
    private const long SpaceSize = 3_521_614_606_208L; // 62^7 = 2^7 * 31^7

    private const string StandardBase62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const string DefaultFallbackSalt = "ShortUrl-Default-Base62-Salt-2026";

    private readonly char[][] _positionAlphabets;
    private readonly int[,] _positionLookup;
    private readonly Int128 _multiplier;
    private readonly Int128 _inverseMultiplier;
    private readonly Int128 _offset;
    private readonly Int128 _modulus = SpaceSize;

    public Base62CodeGenerator(IOptions<ShortUrlOptions> options)
        : this(options?.Value?.HashSalt)
    {
    }

    public Base62CodeGenerator(string? salt)
    {
        var effectiveSalt = string.IsNullOrWhiteSpace(salt) ? DefaultFallbackSalt : salt;

        // 1. Embaralha o alfabeto Base62 de forma determinística estilo Hashids para cada uma das 7 posições
        _positionAlphabets = new char[CodeLength][];
        _positionLookup = new int[CodeLength, 128];

        for (var pos = 0; pos < CodeLength; pos++)
        {
            for (var c = 0; c < 128; c++)
            {
                _positionLookup[pos, c] = -1;
            }

            var posSalt = $"{effectiveSalt}:pos:{pos}";
            var shuffled = ConsistentShuffle(StandardBase62Alphabet, posSalt);
            _positionAlphabets[pos] = shuffled;

            for (var idx = 0; idx < Base; idx++)
            {
                _positionLookup[pos, shuffled[idx]] = idx;
            }
        }

        // 2. Deriva parâmetros afins bijetivos (A, B) em Z_{62^7} a partir do sal secreto (SHA-256)
        // Como 62^7 = 2^7 * 31^7, qualquer A coprimo com 2 e 31 possui inverso multiplicativo único módulo 62^7.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(effectiveSalt));
        var rawA = (long)(BitConverter.ToUInt64(hash, 0) % (ulong)(SpaceSize - 2)) + 1;
        var rawB = (long)(BitConverter.ToUInt64(hash, 8) % (ulong)SpaceSize);

        // Garante gcd(A, 62) == 1 (não divisível por 2 nem por 31) e evita A == 1
        if (rawA % 2 == 0)
        {
            rawA++;
        }

        if (rawA % 31 == 0)
        {
            rawA += 2;
        }

        if (rawA <= 1 || rawA >= SpaceSize)
        {
            rawA = 1_456_789_012_345L; // ímpar e não múltiplo de 31
        }

        _multiplier = rawA;
        _inverseMultiplier = ModInverse(_multiplier, _modulus);
        _offset = rawB;
    }

    /// <inheritdoc />
    public string Encode(long id)
    {
        if (id <= 0 || id > MaxId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"O identificador deve estar no intervalo de 1 até {MaxId}.");
        }

        // Transformação afim bijetiva: y = (A * id + B) mod 62^7
        var permuted = (long)(((_multiplier * id) + _offset) % _modulus);

        Span<char> buffer = stackalloc char[CodeLength];
        var current = permuted;

        for (var pos = CodeLength - 1; pos >= 0; pos--)
        {
            var digit = (int)(current % Base);
            current /= Base;
            buffer[pos] = _positionAlphabets[pos][digit];
        }

        return new string(buffer);
    }

    /// <inheritdoc />
    public long Decode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != CodeLength)
        {
            throw new ArgumentException(
                $"O código curto deve conter exatamente {CodeLength} caracteres Base62.",
                nameof(code));
        }

        long permuted = 0;

        for (var pos = 0; pos < CodeLength; pos++)
        {
            var ch = code[pos];
            if (ch >= 128)
            {
                throw new ArgumentException(
                    $"Caractere inválido '{ch}' encontrado na posição {pos} do código curto.",
                    nameof(code));
            }

            var digit = _positionLookup[pos, ch];
            if (digit < 0)
            {
                throw new ArgumentException(
                    $"Caractere inválido '{ch}' encontrado na posição {pos} do código curto.",
                    nameof(code));
            }

            permuted = (permuted * Base) + digit;
        }

        // Transformação inversa bijetiva: id = A^-1 * (y - B) mod 62^7
        var diff = (Int128)permuted - _offset;
        if (diff < 0)
        {
            diff += _modulus;
        }

        var id = (long)((_inverseMultiplier * diff) % _modulus);

        if (id <= 0 || id > MaxId)
        {
            throw new ArgumentException("O código informado decodifica para um identificador inválido (0).", nameof(code));
        }

        return id;
    }

    /// <summary>
    /// Embaralhamento determinístico consistente (algoritmo Fisher-Yates do Hashids) baseado no sal informado.
    /// </summary>
    private static char[] ConsistentShuffle(string alphabet, string salt)
    {
        var chars = alphabet.ToCharArray();
        var saltBytes = Encoding.UTF8.GetBytes(salt);

        for (int i = chars.Length - 1, v = 0, p = 0; i > 0; i--, v++)
        {
            v %= saltBytes.Length;
            int n = saltBytes[v];
            p += n;
            var j = (n + v + p) % i;

            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return chars;
    }

    /// <summary>
    /// Algoritmo de Euclides Estendido para cálculo do inverso multiplicativo modular de <paramref name="a"/> módulo <paramref name="m"/>.
    /// </summary>
    private static Int128 ModInverse(Int128 a, Int128 m)
    {
        Int128 m0 = m;
        Int128 y = 0, x = 1;

        while (a > 1)
        {
            var q = a / m;
            var t = m;

            m = a % m;
            a = t;
            t = y;

            y = x - (q * y);
            x = t;
        }

        if (x < 0)
        {
            x += m0;
        }

        return x;
    }
}

