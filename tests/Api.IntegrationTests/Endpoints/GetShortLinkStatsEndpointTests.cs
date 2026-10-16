using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ShortUrl.Application.ShortLinks.DTOs;

namespace ShortUrl.Api.IntegrationTests.Endpoints;

public class GetShortLinkStatsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetShortLinkStatsEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetStats_WhenLinkExists_ShouldReturn200OkWithPublicStatsAndZeroInitialAccessCount()
    {
        const string targetUrl = "https://exemplo.com.br/artigos/arquitetura-alta-escala-2026";
        var createResponse = await _client.PostAsJsonAsync("/shorturl-api/links", new { targetUrl });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ShortLinkCreatedDto>();
        Assert.NotNull(created);

        var statsResponse = await _client.GetAsync($"/shorturl-api/links/{created.Code}/stats");

        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await statsResponse.Content.ReadFromJsonAsync<ShortLinkStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(created.Code, stats.Code);
        Assert.Equal(targetUrl, stats.TargetUrl);
        Assert.Equal(created.CreatedAtUtc, stats.CreatedAtUtc);
        Assert.Equal(created.ExpiresAtUtc, stats.ExpiresAtUtc);
        Assert.False(stats.IsExpired);
        Assert.Equal(0L, stats.AccessCount);
    }

    [Fact]
    public async Task GetStats_AfterConcurrentRedirects_ShouldReturnExactTotalAccessCount()
    {
        const string targetUrl = "https://learn.microsoft.com/pt-br/aspnet/core";
        var createResponse = await _client.PostAsJsonAsync("/shorturl-api/links", new { targetUrl });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ShortLinkCreatedDto>();
        Assert.NotNull(created);

        const int totalRedirects = 25;
        var redirectTasks = Enumerable.Range(0, totalRedirects)
            .Select(_ => _client.GetAsync($"/{created.Code}"))
            .ToArray();

        var redirectResponses = await Task.WhenAll(redirectTasks);
        Assert.All(redirectResponses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));

        var statsResponse = await _client.GetAsync($"/shorturl-api/links/{created.Code}/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await statsResponse.Content.ReadFromJsonAsync<ShortLinkStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(created.Code, stats.Code);
        Assert.Equal(targetUrl, stats.TargetUrl);
        Assert.False(stats.IsExpired);
        Assert.Equal(totalRedirects, stats.AccessCount);
    }

    [Fact]
    public async Task GetStats_WhenCodeDoesNotExist_ShouldReturn404NotFoundWithPortugueseProblemDetails()
    {
        var response = await _client.GetAsync("/shorturl-api/links/Z9y8X7w/stats");

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

