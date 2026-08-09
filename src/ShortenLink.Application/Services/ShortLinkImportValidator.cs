using System.Runtime.CompilerServices;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;

namespace ShortenLink.Application.Services;

public sealed class ShortLinkImportValidator(TimeProvider timeProvider) : IShortLinkImportValidator
{
    public async IAsyncEnumerable<ShortLinkImportValidationItem> ValidateAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var seenIdempotencyKeys = new HashSet<string>(StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();
        var itemNumber = 0;

        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            itemNumber++;
            if (itemNumber > ShortLinkImportLimits.MaxDryRunItems)
            {
                break;
            }

            var error = ValidateItem(item, now, seenIdempotencyKeys);
            var result = error is null
                ? new ShortLinkImportItemResponse(itemNumber, true)
                : new ShortLinkImportItemResponse(itemNumber, false, error.Value.Code, error.Value.Message);
            yield return new ShortLinkImportValidationItem(itemNumber, item, result);
            await Task.Yield();
        }
    }

    public async Task<ShortLinkImportDryRunResponse> ValidateDryRunAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var results = new List<ShortLinkImportItemResponse>();
        var truncated = false;
        await foreach (var validation in ValidateAsync(
            TrackTruncationAsync(items, () => truncated = true, cancellationToken),
            cancellationToken))
        {
            results.Add(validation.Result);
        }

        var validCount = results.Count(static result => result.Succeeded);
        return new ShortLinkImportDryRunResponse(
            results.Count,
            validCount,
            results.Count - validCount,
            truncated,
            results);
    }

    private static async IAsyncEnumerable<ShortLinkImportItemRequest> TrackTruncationAsync(
        IAsyncEnumerable<ShortLinkImportItemRequest> items,
        Action markTruncated,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var itemNumber = 0;
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            itemNumber++;
            if (itemNumber > ShortLinkImportLimits.MaxDryRunItems)
            {
                markTruncated();
            }

            yield return item;
        }
    }

    private static (string Code, string Message)? ValidateItem(
        ShortLinkImportItemRequest item,
        DateTimeOffset now,
        ISet<string> seenIdempotencyKeys)
    {
        if (!ShortLinkUrlValidator.TryCreate(item.OriginalUrl, out _))
        {
            return (
                ShortLinkImportErrorCodes.InvalidUrl,
                "Original URL must be an absolute HTTP or HTTPS URL.");
        }

        if (item.ExpiredAtUtc is null || item.ExpiredAtUtc <= now)
        {
            return (
                ShortLinkImportErrorCodes.InvalidExpiration,
                "Expiration must be in the future.");
        }

        if (item.ActiveFromUtc is not null && item.ActiveFromUtc >= item.ExpiredAtUtc)
        {
            return (
                ShortLinkImportErrorCodes.InvalidActivationWindow,
                "Activation must be earlier than expiration.");
        }

        if (item.MaxClicks is <= 0)
        {
            return (
                ShortLinkImportErrorCodes.InvalidMaxClicks,
                "MaxClicks must be a positive integer.");
        }

        if (!ShortLinkPassword.IsValid(item.Password))
        {
            return (
                ShortLinkImportErrorCodes.InvalidPassword,
                $"Password must be non-empty and at most {ShortLinkPassword.MaxLength} characters.");
        }

        if (!ShortLinkIdempotencyKey.IsValid(item.IdempotencyKey))
        {
            return (
                ShortLinkImportErrorCodes.InvalidIdempotencyKey,
                $"Idempotency-Key must be at most {ShortLinkIdempotencyKey.MaxLength} characters.");
        }

        var key = ShortLinkIdempotencyKey.Normalize(item.IdempotencyKey);
        if (key is not null && !seenIdempotencyKeys.Add(key))
        {
            return (
                ShortLinkImportErrorCodes.DuplicateIdempotencyKey,
                "Each Idempotency-Key may appear only once in an import batch.");
        }

        return null;
    }
}
