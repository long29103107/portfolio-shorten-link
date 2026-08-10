using Microsoft.EntityFrameworkCore;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Persistence.Schema;

internal sealed class PostgresSchemaDialect : IDatabaseSchemaDialect
{
    public Task EnsureUtcTimestampSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task EnsureIdempotencySchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"IdempotencyKey\" character varying(256);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"TenantId\" character varying(128) NOT NULL DEFAULT '';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"SharingMode\" integer NOT NULL DEFAULT 1;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN IF NOT EXISTS \"TenantId\" character varying(128) NOT NULL DEFAULT '';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_short_links_IdempotencyKey\";",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_short_links_TenantId_IdempotencyKey\" ON \"short_links\" (\"TenantId\", \"IdempotencyKey\");",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_TenantId_ShortCode\" ON \"short_link_clicks\" (\"TenantId\", \"ShortCode\");",
            cancellationToken);
    }

    public Task EnsureAuditEventsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(AuditEventsSchema, cancellationToken);

    public Task EnsureExpirationCheckpointsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(ExpirationCheckpointsSchema, cancellationToken);

    public async Task EnsureBulkJobsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(BulkJobsSchema, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_bulk_jobs\" ADD COLUMN IF NOT EXISTS \"RequestHash\" character varying(64) NOT NULL DEFAULT '';",
            cancellationToken);
    }

    public Task EnsureScheduledActivationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"ActiveFrom\" timestamp with time zone NULL;",
            cancellationToken);

    public async Task EnsureClickLimitSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"MaxClicks\" integer NULL;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"ClickCount\" integer NOT NULL DEFAULT 0;",
            cancellationToken);
    }

    public Task EnsurePasswordProtectionSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"PasswordHash\" character varying(512);",
            cancellationToken);

    public async Task EnsureAdvancedAnalyticsSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN IF NOT EXISTS \"Device\" character varying(32);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN IF NOT EXISTS \"Browser\" character varying(64);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN IF NOT EXISTS \"OperatingSystem\" character varying(64);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN IF NOT EXISTS \"CountryCode\" character varying(8);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN IF NOT EXISTS \"VisitorKeyHash\" character varying(64);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_ShortCode_VisitorKeyHash\" ON \"short_link_clicks\" (\"ShortCode\", \"VisitorKeyHash\");",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_TenantId_ShortCode_VisitorKeyHash\" ON \"short_link_clicks\" (\"TenantId\", \"ShortCode\", \"VisitorKeyHash\");",
            cancellationToken);
    }

    public async Task EnsureOrganizationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"Folder\" character varying(128);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"short_links\" ADD COLUMN IF NOT EXISTS \"Tags\" character varying(2048) NOT NULL DEFAULT '';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_short_links_Folder\" ON \"short_links\" (\"Folder\");",
            cancellationToken);
    }

    private const string AuditEventsSchema = """
        CREATE TABLE IF NOT EXISTS "short_link_audit_events" (
            "Id" uuid NOT NULL CONSTRAINT "PK_short_link_audit_events" PRIMARY KEY,
            "CreatedBy" uuid NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedBy" uuid NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "ActorId" character varying(256) NOT NULL,
            "Action" character varying(128) NOT NULL,
            "TargetType" character varying(128) NOT NULL,
            "TargetId" character varying(128) NOT NULL,
            "OwnerUserId" character varying(128) NULL,
            "Outcome" character varying(64) NOT NULL,
            "OccurredAt" timestamp with time zone NOT NULL,
            "SubjectUserId" character varying(128) NULL,
            "Detail" character varying(512) NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_short_link_audit_events_OccurredAt_Id"
            ON "short_link_audit_events" ("OccurredAt", "Id");
        CREATE INDEX IF NOT EXISTS "IX_short_link_audit_events_Action"
            ON "short_link_audit_events" ("Action");
        CREATE INDEX IF NOT EXISTS "IX_short_link_audit_events_TargetId"
            ON "short_link_audit_events" ("TargetId");
        CREATE INDEX IF NOT EXISTS "IX_short_link_audit_events_ActorId"
            ON "short_link_audit_events" ("ActorId");
        CREATE INDEX IF NOT EXISTS "IX_short_link_audit_events_OwnerUserId"
            ON "short_link_audit_events" ("OwnerUserId");
        """;

    private const string ExpirationCheckpointsSchema = """
        CREATE TABLE IF NOT EXISTS "short_link_expiration_checkpoints" (
            "Id" uuid NOT NULL CONSTRAINT "PK_short_link_expiration_checkpoints" PRIMARY KEY,
            "CreatedBy" uuid NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedBy" uuid NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "TenantId" character varying(128) NOT NULL,
            "Cursor" character varying(512) NULL,
            "EvaluatedAtUtc" timestamp with time zone NOT NULL,
            "CheckpointUpdatedAtUtc" timestamp with time zone NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_short_link_expiration_checkpoints_TenantId"
            ON "short_link_expiration_checkpoints" ("TenantId");
        """;

    private const string BulkJobsSchema = """
        CREATE TABLE IF NOT EXISTS "short_link_bulk_jobs" (
            "Id" uuid NOT NULL PRIMARY KEY,
            "CreatedAt" timestamp with time zone NOT NULL,
            "CreatedBy" uuid NULL,
            "UpdatedAt" timestamp with time zone NULL,
            "UpdatedBy" uuid NULL,
            "Operation" character varying(32) NOT NULL,
            "CodesJson" text NOT NULL,
            "Folder" character varying(128) NULL,
            "TagsJson" text NOT NULL,
            "Status" character varying(32) NOT NULL,
            "TotalCount" integer NOT NULL,
            "ProcessedCount" integer NOT NULL,
            "SucceededCount" integer NOT NULL,
            "FailedCount" integer NOT NULL,
            "ResultJson" text NULL,
            "Error" character varying(2048) NULL,
            "ActorId" character varying(256) NULL,
            "UserId" character varying(128) NULL,
            "IsAdmin" boolean NOT NULL DEFAULT false,
            "TenantId" character varying(128) NOT NULL DEFAULT '',
            "IdempotencyKey" character varying(256) NULL,
            "RequestHash" character varying(64) NOT NULL DEFAULT '',
            "AttemptCount" integer NOT NULL DEFAULT 0,
            "StartedAtUtc" timestamp with time zone NULL,
            "CompletedAtUtc" timestamp with time zone NULL,
            "LastHeartbeatAtUtc" timestamp with time zone NULL,
            "LeaseExpiresAtUtc" timestamp with time zone NULL,
            "CancellationRequested" boolean NOT NULL DEFAULT false
        );
        CREATE INDEX IF NOT EXISTS "IX_short_link_bulk_jobs_Status_CreatedAt" ON "short_link_bulk_jobs" ("Status", "CreatedAt");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_short_link_bulk_jobs_TenantId_IdempotencyKey" ON "short_link_bulk_jobs" ("TenantId", "IdempotencyKey");
        """;
}
