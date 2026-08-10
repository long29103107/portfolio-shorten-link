using Microsoft.EntityFrameworkCore;
using ShortenLink.Infrastructure.Persistence.Configurations;
using ShortenLink.Infrastructure.Persistence.Entities;

namespace ShortenLink.Infrastructure.Persistence;

public sealed class ShortLinkDbContext : DbContext
{
    public ShortLinkDbContext(DbContextOptions<ShortLinkDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShortLinkPersistenceEntity> ShortLinks => Set<ShortLinkPersistenceEntity>();

    public DbSet<ShortLinkClickPersistenceEntity> ShortLinkClicks => Set<ShortLinkClickPersistenceEntity>();

    public DbSet<ShortLinkSharePersistenceEntity> ShortLinkShares => Set<ShortLinkSharePersistenceEntity>();

    public DbSet<AuditEventPersistenceEntity> AuditEvents => Set<AuditEventPersistenceEntity>();

    public DbSet<ShortLinkExpirationCheckpointPersistenceEntity> ShortLinkExpirationCheckpoints => Set<ShortLinkExpirationCheckpointPersistenceEntity>();

    public DbSet<ShortenLinkSecurityAssignmentPersistenceEntity> SecurityAssignments => Set<ShortenLinkSecurityAssignmentPersistenceEntity>();

    public DbSet<ShortenLinkCustomRolePersistenceEntity> SecurityCustomRoles => Set<ShortenLinkCustomRolePersistenceEntity>();

    public DbSet<ShortenLinkRolePermissionOverridePersistenceEntity> SecurityRolePermissionOverrides => Set<ShortenLinkRolePermissionOverridePersistenceEntity>();

    public DbSet<ShortenLinkSecurityUserPersistenceEntity> SecurityUsers => Set<ShortenLinkSecurityUserPersistenceEntity>();

    public DbSet<ShortenLinkUserApiKeyPersistenceEntity> SecurityUserApiKeys => Set<ShortenLinkUserApiKeyPersistenceEntity>();

    public DbSet<ShortLinkBulkJobPersistenceEntity> ShortLinkBulkJobs => Set<ShortLinkBulkJobPersistenceEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShortLinkDbContext).Assembly);
    }
}
