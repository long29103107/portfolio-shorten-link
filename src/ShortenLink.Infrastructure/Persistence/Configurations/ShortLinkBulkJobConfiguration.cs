using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShortenLink.Infrastructure.Persistence.Entities;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class ShortLinkBulkJobConfiguration : IEntityTypeConfiguration<ShortLinkBulkJobPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortLinkBulkJobPersistenceEntity> entity)
    {
        entity.ToTable("short_link_bulk_jobs");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(job => job.Operation).HasMaxLength(32).IsRequired();
        entity.Property(job => job.CodesJson).HasMaxLength(65536).IsRequired();
        entity.Property(job => job.Folder).HasMaxLength(128);
        entity.Property(job => job.TagsJson).HasMaxLength(8192).IsRequired();
        entity.Property(job => job.Status).HasMaxLength(32).IsRequired();
        entity.Property(job => job.ResultJson).HasMaxLength(262144);
        entity.Property(job => job.Error).HasMaxLength(2048);
        entity.Property(job => job.ActorId).HasMaxLength(256);
        entity.Property(job => job.UserId).HasMaxLength(128);
        entity.Property(job => job.TenantId).HasMaxLength(128).IsRequired();
        entity.Property(job => job.IdempotencyKey).HasMaxLength(256);
        entity.Property(job => job.RequestHash).HasMaxLength(64).IsRequired();
        entity.HasIndex(job => new { job.TenantId, job.ActorId, job.CreatedAt });
        entity.HasIndex(job => new { job.TenantId, job.IdempotencyKey }).IsUnique();
        entity.HasIndex(job => new { job.Status, job.CreatedAt });
    }
}
