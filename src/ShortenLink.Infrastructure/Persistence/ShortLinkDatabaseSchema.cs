using Microsoft.EntityFrameworkCore;

namespace ShortenLink.Infrastructure.Persistence;

/// <summary>
/// Applies small, idempotent compatibility upgrades for databases created by
/// earlier versions that used EnsureCreated instead of migrations.
/// </summary>
public static class ShortLinkDatabaseSchema
{
    public static async Task EnsureUtcTimestampSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        foreach (var (table, columns) in SqliteTimestampColumns)
        {
            if (!await SqliteTableExistsAsync(dbContext, table, cancellationToken))
            {
                continue;
            }

            foreach (var column in columns)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    CreateSqliteTimestampNormalizationSql(table, column),
                    cancellationToken);
            }
        }
    }

    private static string CreateSqliteTimestampNormalizationSql(
        string table,
        string column) =>
        string.Concat(
            "UPDATE \"", table, "\" ",
            "SET \"", column, "\" = strftime('%Y-%m-%d %H:%M:%f0000', \"", column, "\") ",
            "WHERE \"", column, "\" IS NOT NULL ",
            "AND (substr(\"", column, "\", -1) = 'Z' ",
            "OR substr(\"", column, "\", -6, 1) IN ('+', '-'));"
        );

    public static async Task EnsureIdempotencySchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (dbContext.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(
                dbContext,
                "short_links",
                "IdempotencyKey",
                "ALTER TABLE \"short_links\" ADD COLUMN \"IdempotencyKey\" TEXT NULL;",
                cancellationToken);
            await EnsureSqliteColumnAsync(
                dbContext,
                "short_links",
                "TenantId",
                "ALTER TABLE \"short_links\" ADD COLUMN \"TenantId\" TEXT NOT NULL DEFAULT '';",
                cancellationToken);
            await EnsureSqliteColumnAsync(
                dbContext,
                "short_links",
                "SharingMode",
                "ALTER TABLE \"short_links\" ADD COLUMN \"SharingMode\" INTEGER NOT NULL DEFAULT 1;",
                cancellationToken);
            await EnsureSqliteColumnAsync(
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
            if (await SqliteTableExistsAsync(dbContext, "short_link_clicks", cancellationToken))
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS \"IX_short_link_clicks_TenantId_ShortCode\" ON \"short_link_clicks\" (\"TenantId\", \"ShortCode\");",
                    cancellationToken);
            }
            return;
        }

        if (dbContext.Database.IsNpgsql())
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
    }

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

    public static Task EnsureExpirationCheckpointsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var sql = dbContext.Database.IsSqlite()
            ? SqliteExpirationCheckpointsSchema
            : dbContext.Database.IsNpgsql()
                ? PostgresExpirationCheckpointsSchema
                : null;

        return sql is null
            ? Task.CompletedTask
            : dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureSqliteColumnAsync(
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

        if (!tableExists)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            alterSql,
            cancellationToken);
    }

    private static async Task<bool> SqliteTableExistsAsync(
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
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
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

    private const string SqliteExpirationCheckpointsSchema = """
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

    private const string PostgresExpirationCheckpointsSchema = """
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

    private static readonly IReadOnlyDictionary<string, string[]> SqliteTimestampColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["short_links"] = ["CreatedAt", "UpdatedAt", "ExpiresAt"],
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
