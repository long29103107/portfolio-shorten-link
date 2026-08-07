using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventPersistenceEntity> entity)
    {
        entity.ToTable("short_link_audit_events");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.ActorId).HasMaxLength(256).IsRequired();
        entity.Property(item => item.Action).HasMaxLength(128).IsRequired();
        entity.Property(item => item.TargetType).HasMaxLength(128).IsRequired();
        entity.Property(item => item.TargetId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.OwnerUserId).HasMaxLength(128);
        entity.Property(item => item.Outcome).HasMaxLength(64).IsRequired();
        entity.Property(item => item.OccurredAt).IsRequired();
        entity.Property(item => item.SubjectUserId).HasMaxLength(128);
        entity.Property(item => item.Detail).HasMaxLength(512);
        entity.HasIndex(item => new { item.OccurredAt, item.Id });
        entity.HasIndex(item => item.Action);
        entity.HasIndex(item => item.TargetId);
        entity.HasIndex(item => item.ActorId);
        entity.HasIndex(item => item.OwnerUserId);
        entity.HasIndex(item => new { item.OwnerUserId, item.OccurredAt, item.Id })
            .HasDatabaseName("IX_short_link_audit_events_OwnerUserId_OccurredAt_Id");
        entity.HasIndex(item => new { item.TargetType, item.TargetId, item.OccurredAt, item.Id })
            .HasDatabaseName("IX_short_link_audit_events_Target_OccurredAt_Id");
    }
}
