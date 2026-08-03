using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class ShortLinkExpirationEvaluatorTests
{
    [Fact]
    public void Evaluate_UsesSuppliedTimeAndRetentionWithoutMutatingLinks()
    {
        var evaluatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var expired = new ShortLink(
            "expired1",
            new Uri("https://example.com/expired"),
            evaluatedAt.AddDays(-5),
            evaluatedAt.AddDays(-2),
            tenantId: "tenant-a");
        var retained = new ShortLink(
            "retain1",
            new Uri("https://example.com/retain"),
            evaluatedAt.AddDays(-2),
            evaluatedAt.AddHours(-2),
            tenantId: "tenant-a");
        var future = new ShortLink(
            "future1",
            new Uri("https://example.com/future"),
            evaluatedAt,
            evaluatedAt.AddHours(1),
            tenantId: "tenant-a");
        var evaluator = new ShortLinkExpirationEvaluator();

        var result = evaluator.Evaluate(
            [future, expired, retained],
            new ShortLinkExpirationBatchRequest(
                evaluatedAt,
                TenantId: "tenant-a",
                RetentionPolicy: new ShortLinkRetentionPolicy(TimeSpan.FromDays(1))));

        Assert.Equal(
            new[] { "expired1", "retain1", "future1" },
            result.Items.Select(item => item.Code));
        Assert.Equal(ShortLinkExpirationOutcome.Expired, result.Items[0].Outcome);
        Assert.Equal("retention_elapsed", result.Items[0].Reason);
        Assert.True(result.Items[0].CacheInvalidationRequired);
        Assert.Equal(ShortLinkExpirationOutcome.Retained, result.Items[1].Outcome);
        Assert.Equal("retention_window", result.Items[1].Reason);
        Assert.Equal(ShortLinkExpirationOutcome.Retained, result.Items[2].Outcome);
        Assert.True(expired.IsActive);
        Assert.Equal(evaluatedAt.AddDays(-2), expired.ExpiresAt);
    }

    [Fact]
    public void Evaluate_ClampsLimitAndReturnsStableCursor()
    {
        var evaluatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var links = new[]
        {
            Create("a000001", evaluatedAt.AddDays(-3)),
            Create("b000001", evaluatedAt.AddDays(-2)),
            Create("c000001", evaluatedAt.AddDays(-1))
        };
        var evaluator = new ShortLinkExpirationEvaluator();

        var first = evaluator.Evaluate(
            links,
            new ShortLinkExpirationBatchRequest(evaluatedAt, Limit: 1));
        var second = evaluator.Evaluate(
            links,
            new ShortLinkExpirationBatchRequest(
                evaluatedAt,
                Cursor: first.NextCursor,
                Limit: 1));

        Assert.True(first.HasMore);
        Assert.Equal("a000001", Assert.Single(first.Items).Code);
        Assert.True(second.HasMore);
        Assert.Equal("b000001", Assert.Single(second.Items).Code);
    }

    [Fact]
    public void Evaluate_DoesNotReturnAnotherTenantPartition()
    {
        var now = DateTimeOffset.UtcNow;
        var evaluator = new ShortLinkExpirationEvaluator();
        var result = evaluator.Evaluate(
            [
                Create("tenant-a", now.AddDays(-1), "tenant-a"),
                Create("tenant-b", now.AddDays(-1), "tenant-b")
            ],
            new ShortLinkExpirationBatchRequest(now, TenantId: "tenant-a"));

        Assert.Equal("tenant-a", Assert.Single(result.Items).TenantId);
    }

    private static ShortLink Create(
        string code,
        DateTimeOffset expiresAt,
        string? tenantId = null) =>
        new(
            code,
            new Uri("https://example.com/" + code),
            expiresAt.AddDays(-1),
            expiresAt,
            tenantId: tenantId);
}
