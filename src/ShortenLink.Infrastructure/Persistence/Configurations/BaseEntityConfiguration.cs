using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShortenLink.Core.Domain;

namespace ShortenLink.Infrastructure.Persistence.Configurations;

internal static class BaseEntityConfiguration
{
    public static void Apply<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseEntity<Guid>
    {
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.CreatedAt).IsRequired();
        entity.Property(item => item.CreatedBy);
        entity.Property(item => item.UpdatedBy);
        entity.Property(item => item.UpdatedAt);
    }
}
