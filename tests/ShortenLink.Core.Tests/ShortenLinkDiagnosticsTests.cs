using ShortenLink.Core.Diagnostics;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class ShortenLinkDiagnosticsTests
{
    [Fact]
    public void ContractsExposeStableLowCardinalityNames()
    {
        Assert.Equal("ShortenLink", ShortenLinkDiagnostics.ActivitySourceName);
        Assert.Equal("ShortenLink", ShortenLinkDiagnostics.MeterName);
        Assert.Equal("ShortenLink.Redirect", ShortenLinkDiagnostics.RedirectActivityName);
        Assert.Equal("shortenlink.redirects", ShortenLinkDiagnostics.RedirectsMetricName);
        Assert.Equal("shortenlink.redirect.failures", ShortenLinkDiagnostics.RedirectFailuresMetricName);
        Assert.Equal("shortenlink.redirect.cache.hits", ShortenLinkDiagnostics.RedirectCacheHitsMetricName);
        Assert.Equal("shortenlink.redirect.cache.misses", ShortenLinkDiagnostics.RedirectCacheMissesMetricName);
        Assert.DoesNotContain("url", ShortenLinkDiagnostics.CacheHitTagName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("code", ShortenLinkDiagnostics.OutcomeTagName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedirectActivityDoesNotStartWithoutAListener()
    {
        using var activity = ShortenLinkDiagnostics.StartRedirectActivity();

        Assert.Null(activity);
    }
}
