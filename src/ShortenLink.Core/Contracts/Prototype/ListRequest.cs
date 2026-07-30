using System.Text.Json.Serialization;

namespace ShortenLink.Core.Contracts.Prototype;

public class ListRequest : Request
{
    [JsonIgnore]
    public int Count { get; set; }

    public string Fe { get; set; } = string.Empty;

    public string Sort { get; set; } = string.Empty;

    [JsonIgnore]
    public bool OrderDesc => Sort.TrimStart().StartsWith("-", StringComparison.Ordinal);

    [JsonIgnore]
    public string OrderBy => Sort.Trim().TrimStart('+', '-');
}
