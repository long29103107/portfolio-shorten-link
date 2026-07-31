using ShortenLink.Core.Contracts.Requests;

namespace ShortenLink.Core.Contracts.Results;

public sealed record ShortLinkImportItemResult(
    int ItemNumber,
    bool Succeeded,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? ShortCode = null,
    bool Replayed = false);

public sealed record ShortLinkImportDryRunResult(
    int TotalCount,
    int ValidCount,
    int InvalidCount,
    bool Truncated,
    IReadOnlyList<ShortLinkImportItemResult> Items);

public sealed record ShortLinkImportExecutionResult(
    int TotalCount,
    int SucceededCount,
    int FailedCount,
    int ReplayedCount,
    bool Truncated,
    IReadOnlyList<ShortLinkImportItemResult> Items);

public sealed record ShortLinkImportValidationItem(
    int ItemNumber,
    ShortLinkImportItemRequest Item,
    ShortLinkImportItemResult Result);
