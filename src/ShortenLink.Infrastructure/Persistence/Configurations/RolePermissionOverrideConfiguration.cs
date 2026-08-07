using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionOverrideConfiguration : IEntityTypeConfiguration<ShortenLinkRolePermissionOverridePersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortenLinkRolePermissionOverridePersistenceEntity> entity)
    {
        entity.ToTable("shorten_link_security_role_permission_overrides");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.RoleId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.Permission).HasMaxLength(256).IsRequired();
        entity.Property(item => item.IsAllowed).IsRequired();
        entity.HasIndex(item => item.RoleId);
        entity.HasIndex(item => new { item.RoleId, item.Permission }).IsUnique();
    }
}
