using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;
using ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;
using ShortUrl.Application.ShortLinks.Queries.GetShortLinkStats;
using ShortUrl.Application.Validators;
using ShortUrl.Domain.Entities;
using ShortUrl.Domain.Exceptions;
using ShortUrl.Infrastructure.Data;
using ShortUrl.Infrastructure.Services;

namespace ShortUrl.Application.UnitTests.ShortLinks;

public class GetShortLinkStatsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly Base62CodeGenerator _codeGenerator;

    public GetShortLinkStatsQueryHandlerTests()
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
    public async Task Handle_WhenShortLinkExists_ShouldReturnStatsWithCorrectAccessCount()
    {
        var createHandler = new CreateShortLinkCommandHandler(_dbContext, _codeGenerator);
        var created = await createHandler.Handle(
            new CreateShortLinkCommand("https://learn.microsoft.com/dotnet"),
            CancellationToken.None);

        var resolveHandler = new ResolveShortLinkCommandHandler(_dbContext);
        await resolveHandler.Handle(new ResolveShortLinkCommand(created.Code), CancellationToken.None);
        await resolveHandler.Handle(new ResolveShortLinkCommand(created.Code), CancellationToken.None);
        await resolveHandler.Handle(new ResolveShortLinkCommand(created.Code), CancellationToken.None);

        var statsHandler = new GetShortLinkStatsQueryHandler(_dbContext);
        var stats = await statsHandler.Handle(
            new GetShortLinkStatsQuery(created.Code),
            CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(created.Code, stats.Code);
        Assert.Equal("https://learn.microsoft.com/dotnet", stats.TargetUrl);
        Assert.Equal(created.CreatedAtUtc, stats.CreatedAtUtc);
        Assert.Equal(created.ExpiresAtUtc, stats.ExpiresAtUtc);
        Assert.False(stats.IsExpired);
        Assert.Equal(3L, stats.AccessCount);
    }

    [Fact]
    public async Task Handle_WhenShortLinkIsExpired_ShouldReturnIsExpiredTrue()
    {
        var nowUtc = DateTime.UtcNow;
        var expiredEntity = new ShortLink
        {
            Code = "Exp1234",
            TargetUrl = "https://example.com/expired",
            CreatedAtUtc = nowUtc.AddDays(-10),
            ExpiresAtUtc = nowUtc.AddSeconds(-1),
            AccessCount = 7L
        };

        _dbContext.ShortLinks.Add(expiredEntity);
        await _dbContext.SaveChangesAsync();

        var statsHandler = new GetShortLinkStatsQueryHandler(_dbContext);
        var stats = await statsHandler.Handle(
            new GetShortLinkStatsQuery("Exp1234"),
            CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal("Exp1234", stats.Code);
        Assert.True(stats.IsExpired);
        Assert.Equal(7L, stats.AccessCount);
    }

    [Fact]
    public async Task Handle_WhenCodeDoesNotExist_ShouldThrowShortLinkNotFoundException()
    {
        var statsHandler = new GetShortLinkStatsQueryHandler(_dbContext);

        var exception = await Assert.ThrowsAsync<ShortLinkNotFoundException>(() =>
            statsHandler.Handle(new GetShortLinkStatsQuery("NoEx123"), CancellationToken.None));

        Assert.Equal("NoEx123", exception.Code);
        Assert.Contains("não foi encontrado", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cJ09A2x")]
    [InlineData("0123456")]
    [InlineData("AbC")]
    public void Validator_WhenCodeIsValidBase62_ShouldPassValidation(string validCode)
    {
        var validator = new GetShortLinkStatsQueryValidator();
        var result = validator.Validate(new GetShortLinkStatsQuery(validCode));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc-def")]
    [InlineData("12345678")]
    [InlineData("ab cd")]
    public void Validator_WhenCodeIsInvalid_ShouldFailWithPortugueseErrorMessage(string invalidCode)
    {
        var validator = new GetShortLinkStatsQueryValidator();
        var result = validator.Validate(new GetShortLinkStatsQuery(invalidCode));

        Assert.False(result.IsValid);
        Assert.All(result.Errors, error =>
            Assert.Contains("código", error.ErrorMessage, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}

