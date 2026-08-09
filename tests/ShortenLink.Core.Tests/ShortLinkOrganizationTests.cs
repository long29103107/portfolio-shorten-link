using ShortenLink.Core;
using ShortenLink.Core.Services;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class ShortLinkOrganizationTests
{
    [Fact]
    public void Normalize_RemovesWhitespaceLowercasesAndDeduplicatesTags()
    {
        var valid = ShortLinkOrganization.TryNormalize(
            " Campaign ",
            ["Launch", " launch ", "Email"],
            out var folder,
            out var tags,
            out var errorCode,
            out var errorMessage);

        Assert.True(valid);
        Assert.Equal("campaign", folder);
        Assert.Equal(new[] { "launch", "email" }, tags);
        Assert.Null(errorCode);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void Normalize_RejectsOversizedTagSets()
    {
        var valid = ShortLinkOrganization.TryNormalize(
            null,
            Enumerable.Range(1, ShortLinkOrganization.MaxTags + 1).Select(value => $"tag-{value}"),
            out _,
            out _,
            out var errorCode,
            out _);

        Assert.False(valid);
        Assert.Equal(ShortLinkErrorCodes.InvalidTags, errorCode);
    }
}
