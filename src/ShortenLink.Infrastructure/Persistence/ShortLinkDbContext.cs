using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShortenLink.Core.Domain;

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

    public DbSet<ShortLinkAuditEventPersistenceEntity> ShortLinkAuditEvents => Set<ShortLinkAuditEventPersistenceEntity>();

    public DbSet<ShortLinkExpirationCheckpointPersistenceEntity> ShortLinkExpirationCheckpoints => Set<ShortLinkExpirationCheckpointPersistenceEntity>();

    public DbSet<ShortenLinkSecurityAssignmentPersistenceEntity> SecurityAssignments => Set<ShortenLinkSecurityAssignmentPersistenceEntity>();

    public DbSet<ShortenLinkCustomRolePersistenceEntity> SecurityCustomRoles => Set<ShortenLinkCustomRolePersistenceEntity>();

    public DbSet<ShortenLinkRolePermissionOverridePersistenceEntity> SecurityRolePermissionOverrides => Set<ShortenLinkRolePermissionOverridePersistenceEntity>();

    public DbSet<ShortenLinkSecurityUserPersistenceEntity> SecurityUsers => Set<ShortenLinkSecurityUserPersistenceEntity>();

    public DbSet<ShortenLinkUserApiKeyPersistenceEntity> SecurityUserApiKeys => Set<ShortenLinkUserApiKeyPersistenceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ShortLinkPersistenceEntity>(entity =>
        {
            entity.ToTable("short_links");
            ConfigureBaseEntity(entity);
            entity.HasIndex(link => link.Code).IsUnique();

            entity.Property(link => link.Code)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(link => link.OriginalUrl)
                .HasMaxLength(4096)
                .IsRequired();

            entity.Property(link => link.CreatedAt)
                .IsRequired();

            entity.Property(link => link.IsActive)
                .IsRequired();

            entity.Property(link => link.CreatedByUserId)
                .HasMaxLength(128);

            entity.Property(link => link.CreatedByDisplayName)
                .HasMaxLength(256);

            entity.Property(link => link.CreatedByUsername)
                .HasMaxLength(256);

            entity.Property(link => link.IdempotencyKey)
                .HasMaxLength(256);

            entity.Property(link => link.TenantId)
                .HasMaxLength(128)
                .HasDefaultValue(string.Empty)
                .IsRequired();

            entity.Property(link => link.SharingMode)
                .HasConversion<int>()
                .IsRequired();

            entity.HasIndex(link => link.CreatedAt);
            entity.HasIndex(link => link.ExpiresAt);
            entity.HasIndex(link => link.IsActive);
            entity.HasIndex(link => new { link.TenantId, link.IdempotencyKey })
                .HasDatabaseName("IX_short_links_TenantId_IdempotencyKey")
                .IsUnique();
        });

        modelBuilder.Entity<ShortLinkClickPersistenceEntity>(entity =>
        {
            entity.ToTable("short_link_clicks");
            ConfigureBaseEntity(entity);

            entity.Property(click => click.ShortCode)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(click => click.TenantId)
                .HasMaxLength(128)
                .HasDefaultValue(string.Empty)
                .IsRequired();

            entity.Property(click => click.ClickedAtUtc)
                .IsRequired();

            entity.Property(click => click.RemoteIpAddress)
                .HasMaxLength(256);

            entity.Property(click => click.UserAgent)
                .HasMaxLength(1024);

            entity.Property(click => click.Referrer)
                .HasMaxLength(2048);

            entity.HasIndex(click => click.ShortCode);
            entity.HasIndex(click => new { click.TenantId, click.ShortCode });
            entity.HasIndex(click => click.ClickedAtUtc);
            entity.HasIndex(click => new { click.ShortCode, click.ClickedAtUtc });
        });

        modelBuilder.Entity<ShortenLinkSecurityAssignmentPersistenceEntity>(entity =>
        {
            entity.ToTable("shorten_link_security_assignments");
            ConfigureBaseEntity(entity);

            entity.Property(assignment => assignment.CredentialKeyHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(assignment => assignment.Name)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(assignment => assignment.RolesJson)
                .HasColumnName("Roles")
                .IsRequired();

            entity.Property(assignment => assignment.PermissionsJson)
                .HasColumnName("Permissions")
                .IsRequired();

            entity.Property(assignment => assignment.IsEnabled)
                .IsRequired();

            entity.Property(assignment => assignment.CreatedAt)
                .IsRequired();

            entity.HasIndex(assignment => assignment.IsEnabled);
            entity.HasIndex(assignment => assignment.CreatedAt);
            entity.HasIndex(assignment => assignment.CredentialKeyHash).IsUnique();
        });

        modelBuilder.Entity<ShortenLinkCustomRolePersistenceEntity>(entity =>
        {
            entity.ToTable("shorten_link_security_custom_roles");
            ConfigureBaseEntity(entity);

            entity.Property(role => role.RoleId)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(role => role.Name)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(role => role.PermissionsJson)
                .HasColumnName("Permissions")
                .IsRequired();

            entity.Property(role => role.IsEnabled)
                .IsRequired();

            entity.Property(role => role.CreatedAt)
                .IsRequired();

            entity.HasIndex(role => role.Name)
                .IsUnique();
            entity.HasIndex(role => role.RoleId)
                .IsUnique();
            entity.HasIndex(role => role.IsEnabled);
            entity.HasIndex(role => role.CreatedAt);
        });

        modelBuilder.Entity<ShortLinkSharePersistenceEntity>(entity =>
        {
            entity.ToTable("short_link_shares");
            ConfigureBaseEntity(entity);
            entity.Property(share => share.ShortCode).HasMaxLength(128).IsRequired();
            entity.Property(share => share.UserId).HasMaxLength(128).IsRequired();
            entity.Property(share => share.Access).IsRequired();
            entity.Property(share => share.CreatedByUserId).HasMaxLength(128).IsRequired();
            entity.Property(share => share.CreatedAt).IsRequired();
            entity.HasIndex(share => share.UserId);
            entity.HasIndex(share => new { share.ShortCode, share.UserId }).IsUnique();
        });

        modelBuilder.Entity<ShortLinkAuditEventPersistenceEntity>(entity =>
        {
            entity.ToTable("short_link_audit_events");
            ConfigureBaseEntity(entity);
            entity.Property(auditEvent => auditEvent.ActorId).HasMaxLength(256).IsRequired();
            entity.Property(auditEvent => auditEvent.Action).HasMaxLength(128).IsRequired();
            entity.Property(auditEvent => auditEvent.TargetType).HasMaxLength(128).IsRequired();
            entity.Property(auditEvent => auditEvent.TargetId).HasMaxLength(128).IsRequired();
            entity.Property(auditEvent => auditEvent.OwnerUserId).HasMaxLength(128);
            entity.Property(auditEvent => auditEvent.Outcome).HasMaxLength(64).IsRequired();
            entity.Property(auditEvent => auditEvent.OccurredAt).IsRequired();
            entity.Property(auditEvent => auditEvent.SubjectUserId).HasMaxLength(128);
            entity.Property(auditEvent => auditEvent.Detail).HasMaxLength(512);
            entity.HasIndex(auditEvent => new { auditEvent.OccurredAt, auditEvent.Id });
            entity.HasIndex(auditEvent => auditEvent.Action);
            entity.HasIndex(auditEvent => auditEvent.TargetId);
            entity.HasIndex(auditEvent => auditEvent.ActorId);
            entity.HasIndex(auditEvent => auditEvent.OwnerUserId);
        });

        modelBuilder.Entity<ShortLinkExpirationCheckpointPersistenceEntity>(entity =>
        {
            entity.ToTable("short_link_expiration_checkpoints");
            ConfigureBaseEntity(entity);
            entity.Property(checkpoint => checkpoint.TenantId)
                .HasMaxLength(128)
                .IsRequired();
            entity.Property(checkpoint => checkpoint.Cursor)
                .HasMaxLength(512);
            entity.Property(checkpoint => checkpoint.EvaluatedAtUtc)
                .IsRequired();
            entity.Property(checkpoint => checkpoint.CheckpointUpdatedAtUtc)
                .IsRequired();
            entity.HasIndex(checkpoint => checkpoint.TenantId)
                .IsUnique();
        });

        modelBuilder.Entity<ShortenLinkRolePermissionOverridePersistenceEntity>(entity =>
        {
            entity.ToTable("shorten_link_security_role_permission_overrides");
            ConfigureBaseEntity(entity);

            entity.Property(item => item.RoleId)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(item => item.Permission)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(item => item.IsAllowed)
                .IsRequired();

            entity.HasIndex(item => item.RoleId);
            entity.HasIndex(item => new { item.RoleId, item.Permission }).IsUnique();
        });

        modelBuilder.Entity<ShortenLinkSecurityUserPersistenceEntity>(entity =>
        {
            entity.ToTable("shorten_link_security_users");
            ConfigureBaseEntity(entity);

            entity.Property(user => user.UserId)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(user => user.Username)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(user => user.DisplayName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(user => user.PasswordHash)
                .HasMaxLength(1024)
                .IsRequired();

            entity.Property(user => user.RoleIdsJson)
                .HasColumnName("RoleIds")
                .IsRequired();

            entity.Property(user => user.IsEnabled)
                .IsRequired();

            entity.Property(user => user.IsHidden)
                .IsRequired();

            entity.Property(user => user.IsBootstrap)
                .IsRequired();

            entity.Property(user => user.CreatedAt)
                .IsRequired();

            entity.HasIndex(user => user.Username)
                .IsUnique();
            entity.HasIndex(user => user.UserId)
                .IsUnique();
            entity.HasIndex(user => user.IsEnabled);
            entity.HasIndex(user => user.IsHidden);
        });

        modelBuilder.Entity<ShortenLinkUserApiKeyPersistenceEntity>(entity =>
        {
            entity.ToTable("shorten_link_security_user_api_keys");
            ConfigureBaseEntity(entity);

            entity.Property(apiKey => apiKey.ApiKeyId)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(apiKey => apiKey.UserId)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(apiKey => apiKey.DisplayName)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(apiKey => apiKey.KeyHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(apiKey => apiKey.IsEnabled)
                .IsRequired();

            entity.Property(apiKey => apiKey.CreatedAt)
                .IsRequired();

            entity.HasIndex(apiKey => apiKey.UserId);
            entity.HasIndex(apiKey => apiKey.KeyHash)
                .IsUnique();
            entity.HasIndex(apiKey => apiKey.ApiKeyId)
                .IsUnique();
            entity.HasIndex(apiKey => apiKey.IsEnabled);
        });
    }

    private static void ConfigureBaseEntity<TEntity>(EntityTypeBuilder<TEntity> entity)
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
