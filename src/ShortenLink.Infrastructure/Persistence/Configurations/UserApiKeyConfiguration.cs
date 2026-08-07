using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal sealed class UserApiKeyConfiguration : IEntityTypeConfiguration<ShortenLinkUserApiKeyPersistenceEntity>
{
    public void Configure(EntityTypeBuilder<ShortenLinkUserApiKeyPersistenceEntity> entity)
    {
        entity.ToTable("shorten_link_security_user_api_keys");
        BaseEntityConfiguration.Apply(entity);
        entity.Property(item => item.ApiKeyId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
        entity.Property(item => item.DisplayName).HasMaxLength(256).IsRequired();
        entity.Property(item => item.KeyHash).HasMaxLength(128).IsRequired();
        entity.Property(item => item.IsEnabled).IsRequired();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.HasIndex(item => item.UserId);
        entity.HasIndex(item => item.KeyHash).IsUnique();
        entity.HasIndex(item => item.ApiKeyId).IsUnique();
        entity.HasIndex(item => item.IsEnabled);
    }
}
