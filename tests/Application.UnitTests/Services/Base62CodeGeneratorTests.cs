using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ShortUrl.Infrastructure.Configuration;
using ShortUrl.Infrastructure.Services;

namespace ShortUrl.Application.UnitTests.Services;

public sealed partial class Base62CodeGeneratorTests
{
    [GeneratedRegex("^[0-9A-Za-z]{7}$")]
    private static partial Regex Base62SevenCharRegex();

    private static Base62CodeGenerator CreateGenerator(string salt = "test-secret-salt-2026")
    {
        var options = Options.Create(new ShortUrlOptions
        {
            HashSalt = salt
        });

        return new Base62CodeGenerator(options);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(3L)]
    [InlineData(10L)]
    [InlineData(62L)]
    [InlineData(1_000L)]
    [InlineData(1_000_000L)]
    [InlineData(365_000_000_000L)] // Volume projetado para 10 anos (100M URLs/dia)
    [InlineData(Base62CodeGenerator.MaxId)]
    public void Encode_ValidIds_ProducesFixed7CharBase62AndDecodesBijectively(long id)
    {
        // Arrange
        var generator = CreateGenerator();

        // Act
        var code = generator.Encode(id);
        var decodedId = generator.Decode(code);

        // Assert
        Assert.Equal(7, code.Length);
        Assert.Matches(Base62SevenCharRegex(), code);
        Assert.Equal(id, decodedId);
    }

    [Fact]
    public void Encode_SameSalt_IsDeterministic()
    {
        // Arrange
        var generator1 = CreateGenerator("shared-production-salt");
        var generator2 = CreateGenerator("shared-production-salt");

        // Act & Assert
        for (long id = 1; id <= 500; id++)
        {
            var code1 = generator1.Encode(id);
            var code2 = generator2.Encode(id);

            Assert.Equal(code1, code2);
            Assert.Equal(id, generator2.Decode(code1));
        }
    }

    [Fact]
    public void Encode_DifferentSalts_ProducesDistinctObfuscatedCodes()
    {
        // Arrange
        var generatorA = CreateGenerator("salt-alpha-2026");
        var generatorB = CreateGenerator("salt-omega-2026");

        // Act & Assert
        for (long id = 1; id <= 100; id++)
        {
            var codeA = generatorA.Encode(id);
            var codeB = generatorB.Encode(id);

            Assert.NotEqual(codeA, codeB);
            Assert.Equal(id, generatorA.Decode(codeA));
            Assert.Equal(id, generatorB.Decode(codeB));
        }
    }

    [Fact]
    public void Encode_AcrossWideRangesAndSequentialIds_GuaranteesZeroCollisions()
    {
        // Arrange
        var generator = CreateGenerator("collision-verification-salt");
        var generatedCodes = new HashSet<string>(capacity: 25_000, StringComparer.Ordinal);

        // 1. Primeiros 10.000 IDs sequenciais
        for (long id = 1; id <= 10_000; id++)
        {
            var code = generator.Encode(id);
            Assert.Matches(Base62SevenCharRegex(), code);
            Assert.True(generatedCodes.Add(code), $"Colisão detectada para Id={id} com código '{code}'.");
            Assert.Equal(id, generator.Decode(code));
        }

        // 2. Amostras distribuídas até 365 bilhões (escala de 10 anos) e próximo de 62^7 - 1
        for (long i = 1; i <= 10_000; i++)
        {
            var highId = (i * 36_500_000L) + i;
            var code = generator.Encode(highId);
            Assert.Matches(Base62SevenCharRegex(), code);
            Assert.True(generatedCodes.Add(code), $"Colisão detectada para Id={highId} com código '{code}'.");
            Assert.Equal(highId, generator.Decode(code));
        }
    }

    [Fact]
    public void Encode_AdjacentSequentialIds_AreObfuscatedWithoutSequentialPattern()
    {
        // Arrange
        var generator = CreateGenerator("obfuscation-test-salt");

        // Act
        var code1 = generator.Encode(1);
        var code2 = generator.Encode(2);
        var code3 = generator.Encode(3);

        // Assert: IDs adjacentes não devem compartilhar os mesmos 5 caracteres iniciais
        Assert.NotEqual(code1[..5], code2[..5]);
        Assert.NotEqual(code2[..5], code3[..5]);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-999999L)]
    [InlineData(Base62CodeGenerator.MaxId + 1)]
    public void Encode_OutOfRangeId_ThrowsArgumentOutOfRangeException(long invalidId)
    {
        // Arrange
        var generator = CreateGenerator();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Encode(invalidId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123456")]
    [InlineData("12345678")]
    [InlineData("abc-123")]
    [InlineData("abc 123")]
    [InlineData("abc_123")]
    [InlineData("áéíóú12")]
    public void Decode_InvalidCodeFormatOrCharacters_ThrowsArgumentException(string invalidCode)
    {
        // Arrange
        var generator = CreateGenerator();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => generator.Decode(invalidCode));
    }
}

