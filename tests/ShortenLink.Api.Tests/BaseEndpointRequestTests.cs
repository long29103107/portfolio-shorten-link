using ShortenLink.Hosting;
using Xunit;

namespace ShortenLink.Api.Tests;

public sealed class BaseEndpointRequestTests
{
    [Fact]
    public void ShortLinkListRequestKeepsQueryDefaultsOptional()
    {
        var request = new ShortLinkListEndpointRequest { Fe = "(Code eq `x`)", Sort = "-CreatedAt" };

        Assert.Null(request.Page);
        Assert.Equal("(Code eq `x`)", request.Fe);
        Assert.Equal("-CreatedAt", request.Sort);
    }
}
