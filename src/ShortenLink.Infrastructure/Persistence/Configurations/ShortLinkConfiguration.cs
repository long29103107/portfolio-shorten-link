using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class ShortLinkConfiguration : IEntityTypeConfiguration<ShortLinkPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortLinkPersistenceEntity> entity)
    {
        entity.ToTable("short_links");
        BaseEntityConfiguration.Apply(entity);
        entity.HasIndex(link => link.Code).IsUnique();
        entity.Property(link => link.Code).HasMaxLength(128).IsRequired();
        entity.Property(link => link.OriginalUrl).HasMaxLength(4096).IsRequired();
        entity.Property(link => link.CreatedAt).IsRequired();
        entity.Property(link => link.IsActive).IsRequired();
        entity.Property(link => link.CreatedByUserId).HasMaxLength(128);
        entity.Property(link => link.CreatedByDisplayName).HasMaxLength(256);
        entity.Property(link => link.CreatedByUsername).HasMaxLength(256);
        entity.Property(link => link.IdempotencyKey).HasMaxLength(256);
        entity.Property(link => link.TenantId).HasMaxLength(128).HasDefaultValue(string.Empty).IsRequired();
        entity.Property(link => link.SharingMode).HasConversion<int>().IsRequired();
        entity.HasIndex(link => link.CreatedAt);
        entity.HasIndex(link => link.ExpiresAt);
        entity.HasIndex(link => link.IsActive);
        entity.HasIndex(link => new { link.TenantId, link.CreatedAt, link.Code })
            .HasDatabaseName("IX_short_links_TenantId_CreatedAt_Code");
        entity.HasIndex(link => new { link.TenantId, link.ExpiresAt, link.Code })
            .HasDatabaseName("IX_short_links_TenantId_ExpiresAt_Code");
        entity.HasIndex(link => new { link.TenantId, link.IdempotencyKey })
            .HasDatabaseName("IX_short_links_TenantId_IdempotencyKey").IsUnique();
    }
}
