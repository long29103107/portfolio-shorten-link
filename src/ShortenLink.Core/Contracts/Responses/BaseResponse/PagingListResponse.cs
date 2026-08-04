namespace ShortenLink.Core.Contracts.Responses.BaseResponse;

public class PagingListResponse<T> : ListResponse<T> where T : class
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public string? NextUrl { get; set; }

    public string? PrevUrl { get; set; }
}
