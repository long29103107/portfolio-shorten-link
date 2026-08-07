using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class SecurityAssignmentConfiguration : IEntityTypeConfiguration<ShortenLinkSecurityAssignmentPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortenLinkSecurityAssignmentPersistenceEntity> entity)
    {
        entity.ToTable("shorten_link_security_assignments");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.CredentialKeyHash).HasMaxLength(128).IsRequired();
        entity.Property(item => item.Name).HasMaxLength(256).IsRequired();
        entity.Property(item => item.RolesJson).HasColumnName("Roles").IsRequired();
        entity.Property(item => item.PermissionsJson).HasColumnName("Permissions").IsRequired();
        entity.Property(item => item.IsEnabled).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.HasIndex(item => item.IsEnabled);
        entity.HasIndex(item => item.CreatedAt);
        entity.HasIndex(item => item.CredentialKeyHash).IsUnique();
    }
}
