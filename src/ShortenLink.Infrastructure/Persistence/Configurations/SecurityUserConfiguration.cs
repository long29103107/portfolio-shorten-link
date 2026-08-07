using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class SecurityUserConfiguration : IEntityTypeConfiguration<ShortenLinkSecurityUserPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortenLinkSecurityUserPersistenceEntity> entity)
    {
        entity.ToTable("shorten_link_security_users");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.Username).HasMaxLength(256).IsRequired();
        entity.Property(item => item.DisplayName).HasMaxLength(256).IsRequired();
        entity.Property(item => item.PasswordHash).HasMaxLength(1024).IsRequired();
        entity.Property(item => item.RoleIdsJson).HasColumnName("RoleIds").IsRequired();
        entity.Property(item => item.IsEnabled).IsRequired();
        entity.Property(item => item.IsHidden).IsRequired();
        entity.Property(item => item.IsBootstrap).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.HasIndex(item => item.Username).IsUnique();
        entity.HasIndex(item => item.UserId).IsUnique();
        entity.HasIndex(item => item.IsEnabled);
        entity.HasIndex(item => item.IsHidden);
    }
}
