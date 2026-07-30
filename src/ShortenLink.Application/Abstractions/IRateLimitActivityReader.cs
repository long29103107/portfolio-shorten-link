using ShortenLink.Application.Contracts.Responses;

namespace ShortenLink.Application.Abstractions;

public interface IRateLimitActivityReader
{
    RateLimitActivityResponse GetSnapshot();
}
