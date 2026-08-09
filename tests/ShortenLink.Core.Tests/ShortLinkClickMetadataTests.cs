using ShortenLink.Core.Analytics;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class ShortLinkClickMetadataTests
{
    [Fact]
    public void FromRequest_ClassifiesCommonBrowserMetadata()
    {
        var metadata = ShortLinkClickMetadata.FromRequest(
            "127.0.0.1",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1",
            "us");

        Assert.Equal("Mobile", metadata.Device);
        Assert.Equal("Safari", metadata.Browser);
        Assert.Equal("iOS", metadata.OperatingSystem);
        Assert.Equal("US", metadata.CountryCode);
        Assert.NotNull(metadata.VisitorKeyHash);
        Assert.Equal(64, metadata.VisitorKeyHash!.Length);
    }

    [Fact]
    public void VisitorKeyHash_IsStableForTheSameIpAndUserAgent()
    {
        var first = ShortLinkClickMetadataParser.CreateVisitorKeyHash(
            "127.0.0.1",
            "same-agent");
        var second = ShortLinkClickMetadataParser.CreateVisitorKeyHash(
            " 127.0.0.1 ",
            " same-agent ");
        var different = ShortLinkClickMetadataParser.CreateVisitorKeyHash(
            "127.0.0.2",
            "same-agent");

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
        Assert.Null(ShortLinkClickMetadataParser.CreateVisitorKeyHash(null, null));
    }
}
