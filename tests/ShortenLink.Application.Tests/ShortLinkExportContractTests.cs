using System.Text.Json;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Application.Features.ShortLinks.Export;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class ShortLinkExportContractTests
{
    [Theory]
    [InlineData(null, ShortLinkExportLimits.DefaultItems)]
    [InlineData(0, 1)]
    [InlineData(1_001, ShortLinkExportLimits.MaxItems)]
    public void Clamp_EnforcesDocumentedBounds(int? requested, int expected) =>
        Assert.Equal(expected, ShortLinkExportLimits.Clamp(requested));

    [Fact]
    public void FromDomain_SerializesOnlySafeExportFields()
    {
        var shortLink = new ShortLink(
            "safe123",
            new Uri("https://example.com/export"),
            new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero),
            createdByUserId: "private-user-id",
            createdByDisplayName: "Private Name",
            createdByUsername: "private-user",
            idempotencyKey: "private-idempotency-key");

        var json = JsonSerializer.Serialize(
            ShortLinkExportRecord.FromDomain(shortLink, "Owner"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("safe123", json, StringComparison.Ordinal);
        Assert.Contains("Owner", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-user-id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-user", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-idempotency-key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotencyKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdBy", json, StringComparison.OrdinalIgnoreCase);
    }
}
