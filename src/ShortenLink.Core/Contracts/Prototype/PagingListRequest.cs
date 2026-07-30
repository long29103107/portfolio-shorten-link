namespace ShortenLink.Core.Contracts.Prototype;

public class PagingListRequest : ListRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
