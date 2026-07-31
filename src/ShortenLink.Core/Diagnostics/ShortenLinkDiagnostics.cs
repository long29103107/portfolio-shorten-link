using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ShortenLink.Core.Diagnostics;

/// <summary>
/// Stable, low-cardinality diagnostics owned by the reusable short-link core.
/// Hosts can attach listeners or exporters without taking a dependency on the
/// demo API. Diagnostics are only emitted when the hosting option enables them.
/// </summary>
public static class ShortenLinkDiagnostics
{
    public const string ActivitySourceName = "ShortenLink";

    public const string MeterName = "ShortenLink";

    public const string RedirectActivityName = "ShortenLink.Redirect";

    public const string RedirectsMetricName = "shortenlink.redirects";

    public const string RedirectFailuresMetricName = "shortenlink.redirect.failures";

    public const string RedirectCacheHitsMetricName = "shortenlink.redirect.cache.hits";

    public const string RedirectCacheMissesMetricName = "shortenlink.redirect.cache.misses";

    public const string CacheHitTagName = "shortenlink.cache_hit";

    public const string OutcomeTagName = "shortenlink.outcome";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Redirects =
        Meter.CreateCounter<long>(RedirectsMetricName, unit: "redirects");

    private static readonly Counter<long> RedirectFailures =
        Meter.CreateCounter<long>(RedirectFailuresMetricName, unit: "redirects");

    private static readonly Counter<long> RedirectCacheHits =
        Meter.CreateCounter<long>(RedirectCacheHitsMetricName, unit: "redirects");

    private static readonly Counter<long> RedirectCacheMisses =
        Meter.CreateCounter<long>(RedirectCacheMissesMetricName, unit: "redirects");

    public static Activity? StartRedirectActivity() =>
        ActivitySource.StartActivity(RedirectActivityName, ActivityKind.Internal);

    public static void RecordRedirect(bool cacheHit, bool succeeded)
    {
        Redirects.Add(1, new KeyValuePair<string, object?>(CacheHitTagName, cacheHit));
        if (cacheHit)
        {
            RedirectCacheHits.Add(1);
        }
        else
        {
            RedirectCacheMisses.Add(1);
        }

        if (!succeeded)
        {
            RedirectFailures.Add(1);
        }
    }
}
