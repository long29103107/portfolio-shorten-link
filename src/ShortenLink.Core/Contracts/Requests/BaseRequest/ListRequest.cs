using System.Text.Json.Serialization;

namespace ShortenLink.Core.Contracts.Requests.BaseRequest;

public class ListRequest : Request
{
    [JsonIgnore]
    public int? Count { get; set; }

    public string? Fe { get; set; }

    public string? Sort { get; set; }

    [JsonIgnore]
    public bool OrderDesc => Sort?.TrimStart().StartsWith("-", StringComparison.Ordinal) == true;

    [JsonIgnore]
    public string OrderBy => Sort?.Trim().TrimStart('+', '-') ?? string.Empty;
}
