namespace ShortenLink.Core.Contracts.Responses;

public abstract class Response
{
    public int StatusCode { get; set; } = 200;
}
