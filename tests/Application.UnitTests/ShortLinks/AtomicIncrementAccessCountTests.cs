using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;
using ShortUrl.Application.ShortLinks.Commands.ResolveShortLink;
using ShortUrl.Domain.Entities;
using ShortUrl.Domain.Exceptions;
using ShortUrl.Infrastructure.Data;
using ShortUrl.Infrastructure.Services;

namespace ShortUrl.Application.UnitTests.ShortLinks;

public class AtomicIncrementAccessCountTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly Base62CodeGenerator _codeGenerator;

    public AtomicIncrementAccessCountTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"shorturl_atomic_test_{Guid.NewGuid():N}.db");

        _connectionString = $"Data Source={_databasePath};Mode=ReadWriteCreate;Default Timeout=30;";
        _codeGenerator = new Base62CodeGenerator("atomic-unit-test-salt-2026");

        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task ResolveShortLink_UnderConcurrentExecution_ShouldAtomicallyIncrementWithoutLostUpdates()
    {
        string code;
        await using (var setupContext = CreateDbContext())
        {
            var createHandler = new CreateShortLinkCommandHandler(setupContext, _codeGenerator);
            var created = await createHandler.Handle(
                new CreateShortLinkCommand("https://learn.microsoft.com/dotnet/csharp"),
                CancellationToken.None);
            code = created.Code;
        }

        const int concurrentRequests = 50;
        using var startGate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ => Task.Run(async () =>
        {
            startGate.Wait();
            await using var context = CreateDbContext();
            var handler = new ResolveShortLinkCommandHandler(context);
            return await handler.Handle(new ResolveShortLinkCommand(code), CancellationToken.None);
        })).ToArray();

        startGate.Set();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, url => Assert.Equal("https://learn.microsoft.com/dotnet/csharp", url));

        await using var verifyContext = CreateDbContext();
        var updatedLink = await verifyContext.ShortLinks
            .AsNoTracking()
            .SingleAsync(x => x.Code == code);

        Assert.Equal(concurrentRequests, updatedLink.AccessCount);
    }

    [Fact]
    public async Task ResolveShortLink_WhenLinkIsExpired_ShouldThrowShortLinkExpiredExceptionAndNotIncrementAccessCount()
    {
        var nowUtc = DateTime.UtcNow;
        await using (var setupContext = CreateDbContext())
        {
            setupContext.ShortLinks.Add(new ShortLink
            {
                Code = "Exp9999",
                TargetUrl = "https://example.com/expired-resource",
                CreatedAtUtc = nowUtc.AddDays(-5),
                ExpiresAtUtc = nowUtc.AddMinutes(-1),
                AccessCount = 12L
            });
            await setupContext.SaveChangesAsync();
        }

        await using (var resolveContext = CreateDbContext())
        {
            var handler = new ResolveShortLinkCommandHandler(resolveContext);
            await Assert.ThrowsAsync<ShortLinkExpiredException>(() =>
                handler.Handle(new ResolveShortLinkCommand("Exp9999"), CancellationToken.None));
        }

        await using var verifyContext = CreateDbContext();
        var link = await verifyContext.ShortLinks
            .AsNoTracking()
            .SingleAsync(x => x.Code == "Exp9999");

        Assert.Equal(12L, link.AccessCount);
    }

    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(SqliteWalConnectionInterceptor.Instance)
            .Options;

        return new ApplicationDbContext(options);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDeleteFile(_databasePath);
        TryDeleteFile($"{_databasePath}-wal");
        TryDeleteFile($"{_databasePath}-shm");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignora falhas de limpeza em arquivos temporários de teste.
        }
    }
}

