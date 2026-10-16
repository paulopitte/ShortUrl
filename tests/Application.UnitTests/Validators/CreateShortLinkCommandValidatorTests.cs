using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;
using ShortUrl.Application.Validators;

namespace ShortUrl.Application.UnitTests.Validators;

public class CreateShortLinkCommandValidatorTests
{
    private readonly CreateShortLinkCommandValidator _validator = new();

    [Theory]
    [InlineData("https://learn.microsoft.com/pt-br/dotnet")]
    [InlineData("http://example.com/path?query=1#hash")]
    [InlineData("https://sub.dominio.com.br:8443/recurso")]
    [InlineData("https://8.8.8.8/dns")]
    public void Validate_WithValidPublicUrl_ShouldSucceed(string validUrl)
    {
        var command = new CreateShortLinkCommand(validUrl);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyUrl_ShouldFailWithPortugueseMessage(string emptyUrl)
    {
        var command = new CreateShortLinkCommand(emptyUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateShortLinkCommand.TargetUrl) &&
            e.ErrorMessage.Contains("obrigatória", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithUrlExceeding2048Characters_ShouldFailWithPortugueseMessage()
    {
        var longPath = new string('a', 2040);
        var longUrl = $"https://example.com/{longPath}";
        var command = new CreateShortLinkCommand(longUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateShortLinkCommand.TargetUrl) &&
            e.ErrorMessage.Contains("2048", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ftp://example.com/file.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("mailto:user@example.com")]
    [InlineData("not-a-valid-url")]
    [InlineData("/relative/path/only")]
    public void Validate_WithoutHttpOrHttpsScheme_ShouldFailWithPortugueseMessage(string invalidSchemeUrl)
    {
        var command = new CreateShortLinkCommand(invalidSchemeUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateShortLinkCommand.TargetUrl));
    }

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("https://localhost:5000/test")]
    [InlineData("http://api.localhost/v1")]
    [InlineData("http://meu-servidor.local/pagina")]
    [InlineData("https://sistema.internal/painel")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://127.0.0.1:8080/admin")]
    [InlineData("http://127.255.255.254/loopback")]
    [InlineData("http://[::1]:8080/ipv6-loopback")]
    [InlineData("http://0.0.0.0/")]
    public void Validate_WithLocalhostOrLoopbackIp_ShouldFailWithPortugueseMessage(string loopbackUrl)
    {
        var command = new CreateShortLinkCommand(loopbackUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateShortLinkCommand.TargetUrl) &&
            (e.ErrorMessage.Contains("público", StringComparison.OrdinalIgnoreCase) ||
             e.ErrorMessage.Contains("privado", StringComparison.OrdinalIgnoreCase) ||
             e.ErrorMessage.Contains("local", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("http://10.0.0.1/internal")]
    [InlineData("https://10.255.255.255/secret")]
    [InlineData("http://172.16.0.1/router")]
    [InlineData("http://172.31.255.254/private")]
    [InlineData("http://192.168.0.1/admin")]
    [InlineData("https://192.168.1.100:8443/dashboard")]
    public void Validate_WithRfc1918PrivateIp_ShouldFailWithPortugueseMessage(string privateIpUrl)
    {
        var command = new CreateShortLinkCommand(privateIpUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateShortLinkCommand.TargetUrl) &&
            (e.ErrorMessage.Contains("público", StringComparison.OrdinalIgnoreCase) ||
             e.ErrorMessage.Contains("privado", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://169.254.0.1/link-local")]
    [InlineData("http://[fe80::1]/ipv6-link-local")]
    [InlineData("http://[fc00::1]/ipv6-ula")]
    [InlineData("http://[fd12:3456:789a::1]/ipv6-ula")]
    public void Validate_WithLinkLocalOrUniqueLocalIp_ShouldFailWithPortugueseMessage(string linkLocalUrl)
    {
        var command = new CreateShortLinkCommand(linkLocalUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateShortLinkCommand.TargetUrl) &&
            (e.ErrorMessage.Contains("público", StringComparison.OrdinalIgnoreCase) ||
             e.ErrorMessage.Contains("privado", StringComparison.OrdinalIgnoreCase) ||
             e.ErrorMessage.Contains("local", StringComparison.OrdinalIgnoreCase)));
    }
}

