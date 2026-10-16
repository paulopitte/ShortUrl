using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ShortUrl.Application.ShortLinks.DTOs;

namespace ShortUrl.Api.IntegrationTests.Endpoints;

public class RedirectEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RedirectEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetCode_WhenLinkExists_ShouldReturn302FoundWithLocationAndNoCacheHeaders()
    {
        const string targetUrl = "https://learn.microsoft.com/pt-br/dotnet/core/whats-new/dotnet-10/overview";
        var createResponse = await _client.PostAsJsonAsync("/shorturl-api/links", new { targetUrl });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ShortLinkCreatedDto>();
        Assert.NotNull(created);

        var redirectResponse = await _client.GetAsync($"/{created.Code}");

        Assert.Equal(HttpStatusCode.Found, redirectResponse.StatusCode);
        Assert.NotNull(redirectResponse.Headers.Location);
        Assert.Equal(targetUrl, redirectResponse.Headers.Location.OriginalString);

        var cacheControl = redirectResponse.Headers.CacheControl?.ToString() ?? string.Empty;
        Assert.Contains("no-store", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-cache", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must-revalidate", cacheControl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCode_WhenCodeDoesNotExist_ShouldReturn404NotFoundWithPortugueseProblemDetails()
    {
        var response = await _client.GetAsync("/Z9y8X7w");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Contains("não encontrado", root.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não foi encontrado", root.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}

