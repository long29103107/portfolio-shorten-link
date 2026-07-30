using System.Text.Json.Serialization;

namespace ShortenLink.Core.Contracts.Requests;

public abstract class Request
{
    [JsonIgnore]
    public object? ScopedContext { get; set; }
}
