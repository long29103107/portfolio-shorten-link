using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class ShortLinkShareConfiguration : IEntityTypeConfiguration<ShortLinkSharePersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortLinkSharePersistenceEntity> entity)
    {
        entity.ToTable("short_link_shares");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(share => share.ShortCode).HasMaxLength(128).IsRequired();
        entity.Property(share => share.UserId).HasMaxLength(128).IsRequired();
        entity.Property(share => share.Access).IsRequired();
        entity.Property(share => share.CreatedByUserId).HasMaxLength(128).IsRequired();
        entity.Property(share => share.CreatedAt).IsRequired();
        entity.HasIndex(share => share.UserId);
        entity.HasIndex(share => new { share.ShortCode, share.UserId }).IsUnique();
    }
}
