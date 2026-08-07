using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class CustomRoleConfiguration : IEntityTypeConfiguration<ShortenLinkCustomRolePersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortenLinkCustomRolePersistenceEntity> entity)
    {
        entity.ToTable("shorten_link_security_custom_roles");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.RoleId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.Name).HasMaxLength(256).IsRequired();
        entity.Property(item => item.PermissionsJson).HasColumnName("Permissions").IsRequired();
        entity.Property(item => item.IsEnabled).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.HasIndex(item => item.Name).IsUnique();
        entity.HasIndex(item => item.RoleId).IsUnique();
        entity.HasIndex(item => item.IsEnabled);
        entity.HasIndex(item => item.CreatedAt);
    }
}
