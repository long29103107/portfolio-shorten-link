using Microsoft.EntityFrameworkCore;

namespace ShortenLink.Infrastructure.Persistence;

/// <summary>
/// Applies small, idempotent compatibility upgrades for databases created by
/// earlier versions that used EnsureCreated instead of migrations.
/// </summary>
public static class ShortLinkDatabaseSchema
{
    public static Task EnsureAuditEventsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sql = dbContext.Database.IsSqlite()
            ? SqliteAuditEventsSchema
            : dbContext.Database.IsNpgsql()
                ? PostgresAuditEventsSchema
                : null;

        return sql is null
            ? Task.CompletedTask
            : dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private const string SqliteAuditEventsSchema = """
        CREATE TABLE IF NOT EXISTS "short_link_audit_events" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_short_link_audit_events" PRIMARY KEY,
            "CreatedBy" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "ActorId" TEXT NOT NULL,
            "Action" TEXT NOT NULL,
            "TargetType" TEXT NOT NULL,
            "TargetId" TEXT NOT NULL,
            "OwnerUserId" TEXT NULL,
            "Outcome" TEXT NOT NULL,
            "OccurredAt" TEXT NOT NULL,
            "SubjectUserId" TEXT NULL,
            "Detail" TEXT NULL
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

    private const string PostgresAuditEventsSchema = """
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
}
