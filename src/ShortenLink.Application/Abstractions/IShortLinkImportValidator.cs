using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Responses;

namespace ShortenLink.Application.Abstractions;

public interface IShortLinkImportValidator
{
    IAsyncEnumerable<ShortLinkImportValidationItem> ValidateAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        CancellationToken cancellationToken = default);

    Task<ShortLinkImportDryRunResponse> ValidateDryRunAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        CancellationToken cancellationToken = default);
}
