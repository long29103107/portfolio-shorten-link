using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

public sealed class ShortenLinkOptions
{
    public const string SectionName = "ShortenLink";

    public string? BaseUrl { get; set; }

    public ShortenLinkCodeOptions Code { get; set; } = new();

    public ShortenLinkDatabaseOptions Database { get; set; } = new();

    public ShortenLinkRedirectOptions Redirect { get; set; } = new();

    public ShortenLinkAnalyticsOptions Analytics { get; set; } = new();

    public ShortenLinkQueueOptions Queue { get; set; } = new();

    public ShortenLinkCacheOptions Cache { get; set; } = new();

    public ShortenLinkObservabilityOptions Observability { get; set; } = new();

    public ShortenLinkRateLimitingOptions RateLimiting { get; set; } = new();

    public ShortenLinkSecurityOptions Security { get; set; } = new();
}

public sealed class ShortenLinkCodeOptions
{
    public int DefaultLength { get; set; } = 7;

    public int MaxRetry { get; set; } = 10;
}

public sealed class ShortenLinkDatabaseOptions
{
    public bool UsePostgres { get; set; }

    public string SqliteConnectionString { get; set; } = "Data Source=shorten-link.db";

    public string PostgresConnectionString { get; set; } = string.Empty;
}

public sealed class ShortenLinkRedirectOptions
{
    public bool EnableFrontendFallback { get; set; } = true;

    public string FrontendFallbackPath { get; set; } = "/not-found";
}

public sealed class ShortenLinkAnalyticsOptions
{
    public bool Enabled { get; set; }

    public bool UseAsyncWorker { get; set; } = true;

    public int QueueCapacity { get; set; } = 512;
}

public sealed class ShortenLinkCacheOptions
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Memory";

    public string RedisConnectionString { get; set; } = string.Empty;

    public int EntryTtlSeconds { get; set; } = 3600;

    public int NegativeEntryTtlSeconds { get; set; } = 10;
}

public sealed class ShortenLinkQueueOptions
{
    public MessageQueueProvider Provider { get; set; } = MessageQueueProvider.Memory;

    public string? RabbitMqConnectionString { get; set; }

    public string AuditQueueName { get; set; } = "shorten-link.audit";

    public string AnalyticsQueueName { get; set; } = "shorten-link.analytics";

    public int AuditCapacity { get; set; } = 1024;

    public int AnalyticsCapacity { get; set; }

    public ushort PrefetchCount { get; set; } = 16;
}

public sealed class ShortenLinkObservabilityOptions
{
    /// <summary>
    /// Enables the Core ActivitySource and meters for short-link redirects.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Registers the built-in configuration, database, cache, and analytics
    /// health checks with ASP.NET Core health checks.
    /// </summary>
    public bool HealthChecksEnabled { get; set; }
}

public sealed class ShortenLinkRateLimitingOptions
{
    public bool Enabled { get; set; }

    public ShortenLinkFixedWindowRateLimitOptions Create { get; set; } = new();

    public ShortenLinkFixedWindowRateLimitOptions Redirect { get; set; } = new();
}

public sealed class ShortenLinkFixedWindowRateLimitOptions
{
    public int PermitLimit { get; set; } = 60;

    public int WindowSeconds { get; set; } = 60;

    public int QueueLimit { get; set; }
}

public sealed class ShortenLinkSecurityOptions
{
    public bool Enabled { get; set; }

    public string HeaderName { get; set; } = "X-ShortenLink-Api-Key";

    public string SessionSigningKey { get; set; } = string.Empty;

    public int SessionTokenTtlMinutes { get; set; } = 480;

    public int RefreshTokenTtlMinutes { get; set; } = 10080;

    public IList<ShortenLinkApiKeyOptions> ApiKeys { get; set; } = new List<ShortenLinkApiKeyOptions>();
}

public sealed class ShortenLinkApiKeyOptions
{
    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public IList<string> Roles { get; set; } = new List<string>();

    public IList<string> Permissions { get; set; } = new List<string>();
}
