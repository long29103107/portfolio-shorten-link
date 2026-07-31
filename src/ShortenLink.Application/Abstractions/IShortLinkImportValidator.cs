using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Results;

namespace ShortenLink.Application.Abstractions;

public interface IShortLinkImportValidator
{
    IAsyncEnumerable<ShortLinkImportValidationItem> ValidateAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        CancellationToken cancellationToken = default);

    Task<ShortLinkImportDryRunResult> ValidateDryRunAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        CancellationToken cancellationToken = default);
}
