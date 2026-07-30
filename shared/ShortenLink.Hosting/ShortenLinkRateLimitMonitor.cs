using Microsoft.Extensions.Options;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Contracts.Responses;

namespace ShortenLink.Hosting;

public sealed class ShortenLinkRateLimitMonitor(
    IOptions<ShortenLinkOptions> options,
    TimeProvider timeProvider) : IRateLimitActivityReader
{
    private const int RecentLimit = 100;
    private readonly object sync = new();
    private readonly List<RateLimitRejectionResponse> recentRejections = [];
    private long createRejectedCount;
    private long redirectRejectedCount;

    public void RecordRejection(string? policy)
    {
        var normalizedPolicy = policy switch
        {
            ShortenLinkRateLimitingPolicyNames.Create => "create",
            ShortenLinkRateLimitingPolicyNames.Redirect => "redirect",
            _ => "unknown"
        };

        lock (sync)
        {
            if (normalizedPolicy == "create")
            {
                createRejectedCount++;
            }
            else if (normalizedPolicy == "redirect")
            {
                redirectRejectedCount++;
            }

            recentRejections.Insert(0, new RateLimitRejectionResponse(
                normalizedPolicy,
                timeProvider.GetUtcNow()));
            if (recentRejections.Count > RecentLimit)
            {
                recentRejections.RemoveAt(recentRejections.Count - 1);
            }
        }
    }

    public RateLimitActivityResponse GetSnapshot()
    {
        var configured = options.Value.RateLimiting;
        lock (sync)
        {
            return new RateLimitActivityResponse(
                configured.Enabled,
                BuildPolicy(configured.Create, createRejectedCount),
                BuildPolicy(configured.Redirect, redirectRejectedCount),
                recentRejections.ToList());
        }
    }

    private static RateLimitPolicyActivityResponse BuildPolicy(
        ShortenLinkFixedWindowRateLimitOptions configured,
        long rejectedCount) =>
        new(
            configured.PermitLimit,
            configured.WindowSeconds,
            configured.QueueLimit,
            rejectedCount);
}
