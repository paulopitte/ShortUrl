using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShortUrl.Application.ShortLinks.DTOs;

namespace ShortUrl.Api.IntegrationTests.Endpoints;

public class CreateShortLinkEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateShortLinkEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostShortLink_WithValidUrl_ShouldReturn201CreatedWith7CharBase62CodeAndLocationHeader()
    {
        var payload = new
        {
            targetUrl = "https://learn.microsoft.com/dotnet"
        };

        var response = await _client.PostAsJsonAsync("/shorturl-api/links", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<ShortLinkCreatedDto>();
        Assert.NotNull(body);
        Assert.Matches(new Regex("^[0-9A-Za-z]{7}$"), body.Code);
        Assert.Equal($"/{body.Code}", response.Headers.Location.OriginalString);
        Assert.Equal("https://learn.microsoft.com/dotnet", body.TargetUrl);
        Assert.EndsWith($"/{body.Code}", body.ShortUrl);
    }

    [Theory]
    [InlineData("ftp://arquivos.exemplo.com/manual.pdf")]
    [InlineData("http://localhost:8080/internal")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://192.168.1.1/router")]
    [InlineData("http://10.0.0.1/private")]
    [InlineData("http://172.16.0.1/private")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public async Task PostShortLink_WithInvalidOrPrivateUrl_ShouldReturn400BadRequestWithPortugueseProblemDetails(string invalidUrl)
    {
        var payload = new
        {
            targetUrl = invalidUrl
        };

        var response = await _client.PostAsJsonAsync("/shorturl-api/links", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains("validação", root.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(root.TryGetProperty("errors", out var errorsElement));
        Assert.True(errorsElement.TryGetProperty("TargetUrl", out _));
    }
}

