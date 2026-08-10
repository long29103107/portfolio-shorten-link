using Microsoft.EntityFrameworkCore;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Persistence.Schema;

internal sealed class SqliteSchemaDialect : IDatabaseSchemaDialect
{
    public async Task EnsureUtcTimestampSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var (table, columns) in TimestampColumns)
        {
            if (!await TableExistsAsync(dbContext, table, cancellationToken))
            {
                continue;
            }

            foreach (var column in columns)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    CreateTimestampNormalizationSql(table, column),
                    cancellationToken);
            }
        }
    }

    public async Task EnsureIdempotencySchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "IdempotencyKey",
            "ALTER TABLE \"short_links\" ADD COLUMN \"IdempotencyKey\" TEXT NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "TenantId",
            "ALTER TABLE \"short_links\" ADD COLUMN \"TenantId\" TEXT NOT NULL DEFAULT '';",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "SharingMode",
            "ALTER TABLE \"short_links\" ADD COLUMN \"SharingMode\" INTEGER NOT NULL DEFAULT 1;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_link_clicks",
            "TenantId",
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN \"TenantId\" TEXT NOT NULL DEFAULT '';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_short_links_IdempotencyKey\";",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_short_links_TenantId_IdempotencyKey\" ON \"short_links\" (\"TenantId\", \"IdempotencyKey\");",
            cancellationToken);
        if (await TableExistsAsync(dbContext, "short_link_clicks", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_TenantId_ShortCode\" ON \"short_link_clicks\" (\"TenantId\", \"ShortCode\");",
                cancellationToken);
        }
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
        await EnsureColumnAsync(dbContext, "short_link_bulk_jobs", "RequestHash",
            "ALTER TABLE \"short_link_bulk_jobs\" ADD COLUMN \"RequestHash\" TEXT NOT NULL DEFAULT '';",
            cancellationToken);
    }

    public Task EnsureScheduledActivationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) =>
        EnsureColumnAsync(
            dbContext,
            "short_links",
            "ActiveFrom",
            "ALTER TABLE \"short_links\" ADD COLUMN \"ActiveFrom\" TEXT NULL;",
            cancellationToken);

    public async Task EnsureClickLimitSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "MaxClicks",
            "ALTER TABLE \"short_links\" ADD COLUMN \"MaxClicks\" INTEGER NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "ClickCount",
            "ALTER TABLE \"short_links\" ADD COLUMN \"ClickCount\" INTEGER NOT NULL DEFAULT 0;",
            cancellationToken);
    }

    public Task EnsurePasswordProtectionSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) =>
        EnsureColumnAsync(
            dbContext,
            "short_links",
            "PasswordHash",
            "ALTER TABLE \"short_links\" ADD COLUMN \"PasswordHash\" TEXT NULL;",
            cancellationToken);

    public async Task EnsureAdvancedAnalyticsSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "short_link_clicks",
            "Device",
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN \"Device\" TEXT NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_link_clicks",
            "Browser",
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN \"Browser\" TEXT NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_link_clicks",
            "OperatingSystem",
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN \"OperatingSystem\" TEXT NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_link_clicks",
            "CountryCode",
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN \"CountryCode\" TEXT NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_link_clicks",
            "VisitorKeyHash",
            "ALTER TABLE \"short_link_clicks\" ADD COLUMN \"VisitorKeyHash\" TEXT NULL;",
            cancellationToken);
        if (await TableExistsAsync(dbContext, "short_link_clicks", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_ShortCode_VisitorKeyHash\" ON \"short_link_clicks\" (\"ShortCode\", \"VisitorKeyHash\");",
                cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_TenantId_ShortCode_VisitorKeyHash\" ON \"short_link_clicks\" (\"TenantId\", \"ShortCode\", \"VisitorKeyHash\");",
                cancellationToken);
        }
    }

    public async Task EnsureOrganizationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "Folder",
            "ALTER TABLE \"short_links\" ADD COLUMN \"Folder\" TEXT NULL;",
            cancellationToken);
        await EnsureColumnAsync(
            dbContext,
            "short_links",
            "Tags",
            "ALTER TABLE \"short_links\" ADD COLUMN \"Tags\" TEXT NOT NULL DEFAULT '';",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_short_links_Folder\" ON \"short_links\" (\"Folder\");",
            cancellationToken);
    }

    private static string CreateTimestampNormalizationSql(
        string table,
        string column) =>
        string.Concat(
            "UPDATE \"", table, "\" ",
            "SET \"", column, "\" = strftime('%Y-%m-%d %H:%M:%f0000', \"", column, "\") ",
            "WHERE \"", column, "\" IS NOT NULL ",
            "AND (substr(\"", column, "\", -1) = 'Z' ",
            "OR substr(\"", column, "\", -6, 1) IN ('+', '-'));"
        );

    private static async Task EnsureColumnAsync(
        ShortLinkDbContext dbContext,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";
        var tableExists = false;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tableExists = true;
                if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        if (tableExists)
        {
            await dbContext.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(
        ShortLinkDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $table LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$table";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private const string AuditEventsSchema = """
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

    private const string ExpirationCheckpointsSchema = """
        CREATE TABLE IF NOT EXISTS "short_link_expiration_checkpoints" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_short_link_expiration_checkpoints" PRIMARY KEY,
            "CreatedBy" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "TenantId" TEXT NOT NULL,
            "Cursor" TEXT NULL,
            "EvaluatedAtUtc" TEXT NOT NULL,
            "CheckpointUpdatedAtUtc" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_short_link_expiration_checkpoints_TenantId"
            ON "short_link_expiration_checkpoints" ("TenantId");
        """;

    private const string BulkJobsSchema = """
        CREATE TABLE IF NOT EXISTS "short_link_bulk_jobs" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_short_link_bulk_jobs" PRIMARY KEY,
            "CreatedAt" TEXT NOT NULL,
            "CreatedBy" TEXT NULL,
            "UpdatedAt" TEXT NULL,
            "UpdatedBy" TEXT NULL,
            "Operation" TEXT NOT NULL,
            "CodesJson" TEXT NOT NULL,
            "Folder" TEXT NULL,
            "TagsJson" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "TotalCount" INTEGER NOT NULL,
            "ProcessedCount" INTEGER NOT NULL,
            "SucceededCount" INTEGER NOT NULL,
            "FailedCount" INTEGER NOT NULL,
            "ResultJson" TEXT NULL,
            "Error" TEXT NULL,
            "ActorId" TEXT NULL,
            "UserId" TEXT NULL,
            "IsAdmin" INTEGER NOT NULL DEFAULT 0,
            "TenantId" TEXT NOT NULL DEFAULT '',
            "IdempotencyKey" TEXT NULL,
            "RequestHash" TEXT NOT NULL DEFAULT '',
            "AttemptCount" INTEGER NOT NULL DEFAULT 0,
            "StartedAtUtc" TEXT NULL,
            "CompletedAtUtc" TEXT NULL,
            "LastHeartbeatAtUtc" TEXT NULL,
            "LeaseExpiresAtUtc" TEXT NULL,
            "CancellationRequested" INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS "IX_short_link_bulk_jobs_Status_CreatedAt" ON "short_link_bulk_jobs" ("Status", "CreatedAt");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_short_link_bulk_jobs_TenantId_IdempotencyKey" ON "short_link_bulk_jobs" ("TenantId", "IdempotencyKey");
        """;

    private static readonly IReadOnlyDictionary<string, string[]> TimestampColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["short_links"] = ["CreatedAt", "UpdatedAt", "ExpiresAt", "ActiveFrom"],
            ["short_link_clicks"] = ["CreatedAt", "UpdatedAt", "ClickedAtUtc"],
            ["short_link_shares"] = ["CreatedAt", "UpdatedAt"],
            ["short_link_audit_events"] = ["CreatedAt", "UpdatedAt", "OccurredAt"],
            ["short_link_expiration_checkpoints"] =
                ["CreatedAt", "UpdatedAt", "EvaluatedAtUtc", "CheckpointUpdatedAtUtc"],
            ["shorten_link_security_assignments"] = ["CreatedAt", "UpdatedAt"],
            ["shorten_link_security_custom_roles"] = ["CreatedAt", "UpdatedAt"],
            ["shorten_link_security_role_permission_overrides"] = ["CreatedAt", "UpdatedAt"],
            ["shorten_link_security_users"] = ["CreatedAt", "UpdatedAt"],
            ["shorten_link_security_user_api_keys"] = ["CreatedAt", "UpdatedAt"]
        };
}
