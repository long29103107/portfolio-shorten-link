using ShortenLink.Core.Contracts.Requests.BaseRequest;

namespace ShortenLink.Core.Contracts.Requests;

public sealed class ShortLinkListEndpointRequest : ListRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public int? Limit { get; set; }

    public string? Cursor { get; set; }
}
