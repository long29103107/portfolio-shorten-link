using System.Text.Json.Serialization;

namespace ShortenLink.Core.Contracts.Requests.BaseRequest;

public abstract class Request
{
    [JsonIgnore]
    internal object? ScopedContext { get; set; }
}
