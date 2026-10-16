using System.Net;
using System.Text.Json;
using Xunit;

namespace ShortUrl.Api.IntegrationTests.Endpoints;

/// <summary>
/// Testes de integração para os endpoints de Health Checks (/health, /ready) e documentação OpenAPI/Swagger.
/// </summary>
public class HealthAndOpenApiEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthAndOpenApiEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk_WithHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal("Healthy", statusProp.GetString());

        Assert.True(root.TryGetProperty("checks", out var checksProp));
        Assert.True(checksProp.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetReady_ReturnsOk_ValidatingDatabaseAndDiskWrite()
    {
        // Act
        var response = await _client.GetAsync("/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusProp));
        Assert.Equal("Healthy", statusProp.GetString());

        Assert.True(root.TryGetProperty("checks", out var checksProp));
        var checkNames = new List<string>();
        foreach (var element in checksProp.EnumerateArray())
        {
            if (element.TryGetProperty("name", out var nameProp))
            {
                checkNames.Add(nameProp.GetString()!);
            }
        }

        Assert.Contains("sqlite_db", checkNames);
        Assert.Contains("disk_write", checkNames);
    }

    [Fact]
    public async Task GetOpenApiSpecification_ReturnsOk_WithDocumentDetails()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("openapi", out _) || root.TryGetProperty("swagger", out _));
        Assert.True(root.TryGetProperty("info", out var infoProp));
        Assert.Equal("ShortUrl API", infoProp.GetProperty("title").GetString());
        Assert.Equal("v1", infoProp.GetProperty("version").GetString());
    }

    [Fact]
    public async Task GetSwaggerUi_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger", content, StringComparison.OrdinalIgnoreCase);
    }
}

