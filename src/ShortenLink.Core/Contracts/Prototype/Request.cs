using System.Text.Json.Serialization;

namespace ShortenLink.Core.Contracts.Prototype;

/// <summary>
/// Prototype of the shared request base used by MyBlog.Api, kept framework-free
/// so it can be reviewed before becoming part of the public contract surface.
/// </summary>
public abstract class Request
{
    [JsonIgnore]
    public object? ScopedContext { get; set; }
}
