using ShortenLink.Core.Contracts.Requests;

namespace ShortenLink.Core.Contracts.Responses;

public sealed record ShortLinkImportItemResponse(
    int ItemNumber,
    bool Succeeded,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? ShortCode = null,
    bool Replayed = false);

public sealed record ShortLinkImportDryRunResponse(
    int TotalCount,
    int ValidCount,
    int InvalidCount,
    bool Truncated,
    IReadOnlyList<ShortLinkImportItemResponse> Items);

public sealed record ShortLinkImportExecutionResponse(
    int TotalCount,
    int SucceededCount,
    int FailedCount,
    int ReplayedCount,
    bool Truncated,
    IReadOnlyList<ShortLinkImportItemResponse> Items);

public sealed record ShortLinkImportValidationItem(
    int ItemNumber,
    ShortLinkImportItemRequest Item,
    ShortLinkImportItemResponse Result);
