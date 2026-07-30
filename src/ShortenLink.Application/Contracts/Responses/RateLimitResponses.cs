namespace ShortenLink.Application.Contracts.Responses;

public sealed record RateLimitActivityResponse(
    bool Enabled,
    RateLimitPolicyActivityResponse Create,
    RateLimitPolicyActivityResponse Redirect,
    IReadOnlyList<RateLimitRejectionResponse> RecentRejections);

public sealed record RateLimitPolicyActivityResponse(
    int PermitLimit,
    int WindowSeconds,
    int QueueLimit,
    long RejectedCount);

public sealed record RateLimitRejectionResponse(
    string Policy,
    DateTimeOffset OccurredAtUtc);
