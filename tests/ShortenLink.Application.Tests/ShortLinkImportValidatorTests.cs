using ShortenLink.Application.Services;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Services;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class ShortLinkImportValidatorTests
{
    [Fact]
    public async Task ValidateDryRunAsync_ReturnsStablePerItemErrorsWithoutInputEcho()
    {
        var validator = new ShortLinkImportValidator(
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
        var secretUrl = "https://example.com/private-import-secret";

        var result = await validator.ValidateDryRunAsync(Items(
            new ShortLinkImportItemRequest("https://example.com/valid", new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), "key-1"),
            new ShortLinkImportItemRequest("ftp://example.com/invalid", new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), "key-2"),
            new ShortLinkImportItemRequest(secretUrl, new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), "key-1")));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.ValidCount);
        Assert.Equal(2, result.InvalidCount);
        Assert.False(result.Truncated);
        Assert.Equal(ShortLinkImportErrorCodes.InvalidUrl, result.Items[1].ErrorCode);
        Assert.Equal(ShortLinkImportErrorCodes.DuplicateIdempotencyKey, result.Items[2].ErrorCode);
        Assert.DoesNotContain(secretUrl, string.Join('|', result.Items.Select(item => item.ErrorMessage)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateDryRunAsync_BoundsAsyncInput()
    {
        var validator = new ShortLinkImportValidator(
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));

        var result = await validator.ValidateDryRunAsync(
            Items(Enumerable.Range(0, ShortLinkImportLimits.MaxDryRunItems + 1)
                .Select(index => new ShortLinkImportItemRequest(
                    $"https://example.com/{index}",
                    new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)))
                .ToArray()));

        Assert.True(result.Truncated);
        Assert.Equal(ShortLinkImportLimits.MaxDryRunItems, result.TotalCount);
        Assert.Equal(ShortLinkImportLimits.MaxDryRunItems, result.ValidCount);
        Assert.DoesNotContain(result.Items, item => !item.Succeeded);
    }

    private static async IAsyncEnumerable<ShortLinkImportItemRequest> Items(
        params ShortLinkImportItemRequest[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
