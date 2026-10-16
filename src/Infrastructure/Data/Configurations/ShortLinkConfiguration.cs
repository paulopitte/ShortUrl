using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShortUrl.Domain.Entities;

namespace ShortUrl.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração de mapeamento do Entity Framework Core para a entidade <see cref="ShortLink"/>.
/// </summary>
public sealed class ShortLinkConfiguration : IEntityTypeConfiguration<ShortLink>
{
    public void Configure(EntityTypeBuilder<ShortLink> builder)
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
    }
}

