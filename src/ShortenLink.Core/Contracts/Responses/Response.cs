namespace ShortenLink.Core.Contracts.Responses;

using System.Text.Json.Serialization;

public abstract class Response
{
    [JsonIgnore]
    public int StatusCode { get; set; } = 200;
}
