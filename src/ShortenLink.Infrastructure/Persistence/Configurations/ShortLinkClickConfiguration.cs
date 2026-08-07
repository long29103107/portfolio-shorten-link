using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class ShortLinkClickConfiguration : IEntityTypeConfiguration<ShortLinkClickPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortLinkClickPersistenceEntity> entity)
    {
        entity.ToTable("short_link_clicks");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(click => click.ShortCode).HasMaxLength(128).IsRequired();
        entity.Property(click => click.TenantId).HasMaxLength(128).HasDefaultValue(string.Empty).IsRequired();
        entity.Property(click => click.ClickedAtUtc).IsRequired();
        entity.Property(click => click.RemoteIpAddress).HasMaxLength(256);
        entity.Property(click => click.UserAgent).HasMaxLength(1024);
        entity.Property(click => click.Referrer).HasMaxLength(2048);
        entity.HasIndex(click => click.ShortCode);
        entity.HasIndex(click => new { click.TenantId, click.ShortCode });
        entity.HasIndex(click => click.ClickedAtUtc);
        entity.HasIndex(click => new { click.ShortCode, click.ClickedAtUtc });
        entity.HasIndex(click => new { click.TenantId, click.ShortCode, click.ClickedAtUtc })
            .HasDatabaseName("IX_short_link_clicks_TenantId_ShortCode_ClickedAtUtc");
    }
}
