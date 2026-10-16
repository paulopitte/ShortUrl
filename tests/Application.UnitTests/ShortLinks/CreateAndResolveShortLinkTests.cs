using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;
using ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;
using ShortUrl.Domain.Exceptions;
using ShortUrl.Infrastructure.Data;
using ShortUrl.Infrastructure.Services;

namespace ShortUrl.Application.UnitTests.ShortLinks;

public class CreateAndResolveShortLinkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly Base62CodeGenerator _codeGenerator;

    public CreateAndResolveShortLinkTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _codeGenerator = new Base62CodeGenerator("unit-test-secret-salt-2026");
    }

    [Fact]
    public async Task CreateShortLink_ShouldPersistEntityAndGenerate7CharBase62Code()
    {
        var handler = new CreateShortLinkCommandHandler(_dbContext, _codeGenerator);
        const string targetUrl = "https://learn.microsoft.com/dotnet";
        var command = new CreateShortLinkCommand(targetUrl, BaseUrl: "https://sho.rt");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(7, result.Code.Length);
        Assert.Matches(new Regex("^[0-9A-Za-z]{7}$"), result.Code);
        Assert.Equal($"https://sho.rt/{result.Code}", result.ShortUrl);
        Assert.Equal(targetUrl, result.TargetUrl);

        var persisted = await _dbContext.ShortLinks.SingleOrDefaultAsync(x => x.Code == result.Code);
        Assert.NotNull(persisted);
        Assert.Equal(1L, persisted.Id);
        Assert.Equal(_codeGenerator.Encode(1L), persisted.Code);
        Assert.Equal(targetUrl, persisted.TargetUrl);
        Assert.Equal(0L, persisted.AccessCount);
    }

    [Fact]
    public async Task ResolveShortLink_WhenCodeExists_ShouldReturnTargetUrl()
    {
        var createHandler = new CreateShortLinkCommandHandler(_dbContext, _codeGenerator);
        var created = await createHandler.Handle(
            new CreateShortLinkCommand("https://example.com/artigo"),
            CancellationToken.None);

        var resolveHandler = new ResolveShortLinkCommandHandler(_dbContext);
        var resolvedUrl = await resolveHandler.Handle(
            new ResolveShortLinkCommand(created.Code),
            CancellationToken.None);

        Assert.Equal("https://example.com/artigo", resolvedUrl);
    }

    [Fact]
    public async Task ResolveShortLink_WhenCodeDoesNotExist_ShouldThrowShortLinkNotFoundException()
    {
        var resolveHandler = new ResolveShortLinkCommandHandler(_dbContext);

        var exception = await Assert.ThrowsAsync<ShortLinkNotFoundException>(() =>
            resolveHandler.Handle(new ResolveShortLinkCommand("Ab12Xy9"), CancellationToken.None));

        Assert.Equal("Ab12Xy9", exception.Code);
        Assert.Contains("não foi encontrado", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}

