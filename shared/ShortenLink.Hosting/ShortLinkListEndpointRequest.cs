using ShortenLink.Core.Contracts.Requests.BaseRequest;

namespace ShortenLink.Hosting;

public sealed class ShortLinkListEndpointRequest : ListRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public int? Limit { get; set; }

    public string? Cursor { get; set; }

    public string? Search { get; set; }

    public string? Status { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }
}
