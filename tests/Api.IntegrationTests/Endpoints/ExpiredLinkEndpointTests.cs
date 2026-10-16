using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Application.ShortLinks.DTOs;
using ShortUrl.Domain.Entities;

namespace ShortUrl.Api.IntegrationTests.Endpoints;

public class ExpiredLinkEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ExpiredLinkEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetCode_WhenLinkIsExpired_ShouldReturn410GoneAndNotIncrementAccessCount()
    {
        const string expiredCode = "Ex1pRd0";
        const long initialAccessCount = 3L;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var existing = await db.ShortLinks.FindAsync(99901L);
            if (existing is null)
            {
                db.ShortLinks.Add(new ShortLink
                {
                    Id = 99901L,
                    Code = expiredCode,
                    TargetUrl = "https://example.com/already-expired",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
                    AccessCount = initialAccessCount
                });
                await db.SaveChangesAsync();
            }
        }

        var redirectResponse = await _client.GetAsync($"/{expiredCode}");

        Assert.Equal(HttpStatusCode.Gone, redirectResponse.StatusCode);
        Assert.Equal("application/problem+json", redirectResponse.Content.Headers.ContentType?.MediaType);

        var json = await redirectResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(410, root.GetProperty("status").GetInt32());
        Assert.Contains("expirado", root.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expirou", root.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        var statsResponse = await _client.GetAsync($"/shorturl-api/links/{expiredCode}/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await statsResponse.Content.ReadFromJsonAsync<ShortLinkStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(expiredCode, stats.Code);
        Assert.True(stats.IsExpired);
        Assert.Equal(initialAccessCount, stats.AccessCount);
    }

    [Fact]
    public async Task CreateLink_WhenExpiresAtUtcIsInThePast_ShouldReturn400BadRequestWithPortugueseProblemDetails()
    {
        var payload = new
        {
            targetUrl = "https://example.com/future-content",
            expiresAtUtc = DateTime.UtcNow.AddDays(-2)
        };

        var response = await _client.PostAsJsonAsync("/shorturl-api/links", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("ExpiresAtUtc", out var expirationErrors));
        Assert.Contains(expirationErrors.EnumerateArray(), e =>
            e.GetString()!.Contains("A data de expiração deve ser uma data futura em UTC.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateLink_WhenExpiresAtUtcIsOmitted_ShouldDefaultToTenYearsFromCreation()
    {
        var payload = new
        {
            targetUrl = "https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings"
        };

        var response = await _client.PostAsJsonAsync("/shorturl-api/links", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ShortLinkCreatedDto>();
        Assert.NotNull(created);

        var approximateTenYears = created.CreatedAtUtc.AddYears(10);
        var difference = (created.ExpiresAtUtc - approximateTenYears).Duration();
        Assert.True(difference < TimeSpan.FromSeconds(5), $"Expected ~10 years expiration, got {created.ExpiresAtUtc}");
    }

    [Fact]
    public async Task CreateLink_WhenExpiresAtUtcIsProvided_ShouldNormalizeToEndOfDayUtcAndRedirectWhileActive()
    {
        var futureTargetDate = DateTime.UtcNow.AddDays(15).Date;
        var payload = new
        {
            targetUrl = "https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis",
            expiresAtUtc = futureTargetDate
        };

        var response = await _client.PostAsJsonAsync("/shorturl-api/links", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ShortLinkCreatedDto>();
        Assert.NotNull(created);

        Assert.Equal(23, created.ExpiresAtUtc.Hour);
        Assert.Equal(59, created.ExpiresAtUtc.Minute);
        Assert.Equal(59, created.ExpiresAtUtc.Second);
        Assert.Equal(DateTimeKind.Utc, created.ExpiresAtUtc.Kind);

        var redirectResponse = await _client.GetAsync($"/{created.Code}");
        Assert.Equal(HttpStatusCode.Found, redirectResponse.StatusCode);

        var statsResponse = await _client.GetAsync($"/shorturl-api/links/{created.Code}/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<ShortLinkStatsDto>();
        Assert.NotNull(stats);
        Assert.False(stats.IsExpired);
        Assert.Equal(1L, stats.AccessCount);
    }
}

