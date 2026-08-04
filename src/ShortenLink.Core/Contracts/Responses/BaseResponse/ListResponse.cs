namespace ShortenLink.Core.Contracts.Responses.BaseResponse;

using System.Text.Json.Serialization;

public class ListResponse<T> : Response where T : class
{
    public int Count { get; set; }

    public IReadOnlyList<T> Results { get; set; } = [];
}
