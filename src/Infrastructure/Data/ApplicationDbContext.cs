using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShortUrl.Application.Common.Interfaces;
using ShortUrl.Domain.Entities;

namespace ShortUrl.Infrastructure.Data;

/// <summary>
/// Contexto do Entity Framework Core para persistência em SQLite com suporte a modo WAL (Write-Ahead Logging).
/// </summary>
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(SqliteWalConnectionInterceptor.Instance);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ShortLink>(builder =>
        {
            builder.ToTable("ShortLinks");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            builder.Property(e => e.Code)
                .HasMaxLength(7)
                .IsFixedLength()
                .IsRequired();

            builder.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("IX_ShortLinks_Code");

            builder.Property(e => e.TargetUrl)
                .HasMaxLength(2048)
                .IsRequired();

            builder.Property(e => e.CreatedAtUtc)
                .IsRequired();

            builder.Property(e => e.ExpiresAtUtc)
                .IsRequired();

            builder.HasIndex(e => e.ExpiresAtUtc)
                .HasDatabaseName("IX_ShortLinks_ExpiresAtUtc");

            builder.Property(e => e.AccessCount)
                .HasDefaultValue(0L)
                .IsRequired();
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

/// <summary>
/// Interceptor de conexão SQLite que aplica automaticamente PRAGMA journal_mode = WAL; e PRAGMA synchronous = NORMAL;
/// sempre que uma conexão é aberta.
/// </summary>
public sealed class SqliteWalConnectionInterceptor : DbConnectionInterceptor
{
    public static readonly SqliteWalConnectionInterceptor Instance = new();

    private const string WalPragmaCommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ConfigureWalPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ConfigureWalPragmasAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void ConfigureWalPragmas(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = WalPragmaCommandText;
        command.ExecuteNonQuery();
    }

    private static async Task ConfigureWalPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = WalPragmaCommandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

