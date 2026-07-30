using System.Text.Json;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Responses;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class BaseContractTests
{
    [Fact]
    public void PagingRequestUsesStableDefaultsAndSortMetadata()
    {
        var request = new PagingListRequest { Sort = "-CreatedAt" };

        Assert.Equal(1, request.Page);
        Assert.Equal(50, request.PageSize);
        Assert.True(request.OrderDesc);
        Assert.Equal("CreatedAt", request.OrderBy);
    }

    [Fact]
    public void ResponseStatusMetadataDoesNotChangeJsonContract()
    {
        var response = new ListResponse<Item>
        {
            Count = 1,
            Results = [new Item("ok")]
        };

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("Results", json, StringComparison.Ordinal);
        Assert.DoesNotContain("statusCode", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Item(string Value);
}
