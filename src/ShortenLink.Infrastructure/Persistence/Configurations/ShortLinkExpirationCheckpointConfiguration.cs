using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class ShortLinkExpirationCheckpointConfiguration : IEntityTypeConfiguration<ShortLinkExpirationCheckpointPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortLinkExpirationCheckpointPersistenceEntity> entity)
    {
        entity.ToTable("short_link_expiration_checkpoints");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.TenantId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.Cursor).HasMaxLength(512);
        entity.Property(item => item.EvaluatedAtUtc).IsRequired();
        entity.Property(item => item.CheckpointUpdatedAtUtc).IsRequired();
        entity.HasIndex(item => item.TenantId).IsUnique();
    }
}
