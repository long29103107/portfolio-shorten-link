using System.Diagnostics;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Services;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Events;
using ShortenLink.Core.Diagnostics;

namespace ShortenLink.Application.Services;

public sealed class ShortLinkService : IShortLinkService
{
    private readonly IShortLinkRepository repository;
    private readonly IShortLinkCache cache;
    private readonly IShortCodeGenerator codeGenerator;
    private readonly TimeProvider timeProvider;
    private readonly int codeLength;
    private readonly int maxCodeGenerationAttempts;
    private readonly IShortLinkEventSink? eventSink;
    private readonly bool diagnosticsEnabled;

    public ShortLinkService(
        IShortLinkRepository repository,
        IShortCodeGenerator codeGenerator,
        IShortLinkCache? cache = null,
        TimeProvider? timeProvider = null,
        int codeLength = Base62ShortCodeGenerator.DefaultCodeLength,
        int maxCodeGenerationAttempts = 10,
        IShortLinkEventSink? eventSink = null,
        bool diagnosticsEnabled = false)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
        this.cache = cache ?? new DisabledShortLinkCache();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        if (codeLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codeLength), codeLength, "Code length must be greater than zero.");
        }

        if (maxCodeGenerationAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCodeGenerationAttempts), maxCodeGenerationAttempts, "Maximum code generation attempts must be greater than zero.");
        }

        this.codeLength = codeLength;
        this.maxCodeGenerationAttempts = maxCodeGenerationAttempts;
        this.eventSink = eventSink;
        this.diagnosticsEnabled = diagnosticsEnabled;
    }

    public Task<IReadOnlyList<ShortLink>> ListRecentAsync(
        int limit = 100,
        DateTimeOffset? beforeCreatedAt = null,
        string? beforeCode = null,
        CancellationToken cancellationToken = default) =>
        repository.ListRecentAsync(Math.Clamp(limit, 1, 500), beforeCreatedAt, beforeCode, cancellationToken);

    public Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
        int skip,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        repository.ListRecentPageAsync(Math.Max(skip, 0), Math.Clamp(limit, 1, 500), cancellationToken);

    public Task<IReadOnlyList<ShortLink>> ListAccessibleRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default) =>
        repository.ListAccessibleRecentAsync(
            Math.Clamp(limit, 1, 500),
            beforeCreatedAt,
            beforeCode,
            accessScope,
            cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        repository.CountAsync(cancellationToken);

    public Task<ShortLinkListPage> ListPageAsync(
        int skip,
        int limit,
        string? search,
        ShortLinkListStatus status,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var query = new ShortLinkListQuery(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            status,
            sortBy,
            sortDirection,
            now,
            now.AddDays(7));

        return repository.ListPageAsync(
            Math.Max(skip, 0),
            Math.Clamp(limit, 1, 500),
            query,
            cancellationToken);
    }

    public Task<ShortLinkListPage> ListAccessiblePageAsync(
        int skip,
        int limit,
        string? search,
        ShortLinkListStatus status,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accessScope);
        var now = timeProvider.GetUtcNow();
        var query = new ShortLinkListQuery(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            status,
            sortBy,
            sortDirection,
            now,
            now.AddDays(7),
            accessScope);
        return repository.ListPageAsync(
            Math.Max(skip, 0),
            Math.Clamp(limit, 1, 500),
            query,
            cancellationToken);
    }

    public async Task<CreateShortLinkResult> CreateAsync(
        CreateShortLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ShortLinkUrlValidator.TryCreate(request.OriginalUrl, out var originalUrl))
        {
            return CreateShortLinkResult.Failure(
                ShortLinkErrorCodes.InvalidUrl,
                "Original URL must be an absolute HTTP or HTTPS URL.");
        }

        if (!ShortLinkIdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return CreateShortLinkResult.Failure(
                ShortLinkErrorCodes.InvalidIdempotencyKey,
                $"Idempotency-Key must be at most {ShortLinkIdempotencyKey.MaxLength} characters.");
        }

        var idempotencyKey = ShortLinkIdempotencyKey.Normalize(request.IdempotencyKey);
        var idempotencyRepository = repository as IShortLinkIdempotencyRepository;
        if (idempotencyKey is not null && idempotencyRepository is null)
        {
            return CreateShortLinkResult.Failure(
                ShortLinkErrorCodes.IdempotencyNotSupported,
                "The configured persistence provider does not support idempotent creates.");
        }

        var now = timeProvider.GetUtcNow();
        if (request.ExpiresAt is null)
        {
            return CreateShortLinkResult.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration is required.");
        }

        if (request.ExpiresAt <= now)
        {
            return CreateShortLinkResult.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration must be in the future.");
        }

        if (idempotencyKey is not null)
        {
            var existing = await idempotencyRepository!
                .FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            var replay = ResolveIdempotencyReplay(
                existing,
                originalUrl,
                request.ExpiresAt.Value,
                request.CreatedByUserId);
            if (replay is not null)
            {
                return replay;
            }
        }

        for (var attempt = 0; attempt < maxCodeGenerationAttempts; attempt++)
        {
            var code = codeGenerator.Generate(codeLength);
            if (!ShortCodeValidator.IsValid(code)
                || await repository.ExistsByCodeAsync(code, cancellationToken))
            {
                continue;
            }

            var shortLink = new ShortLink(
                code,
                originalUrl,
                now,
                request.ExpiresAt.Value,
                createdByUserId: request.CreatedByUserId,
                createdByDisplayName: request.CreatedByDisplayName,
                createdByUsername: request.CreatedByUsername,
                idempotencyKey: idempotencyKey);

            try
            {
                await repository.AddAsync(shortLink, cancellationToken);
                PublishEvent(ShortLinkEventTypes.Created, shortLink, cancellationToken);
                return CreateShortLinkResult.Success(shortLink);
            }
            catch (ShortLinkCodeConflictException)
            {
                // Another writer won the race after ExistsByCodeAsync. Generate
                // a fresh candidate while preserving unrelated persistence errors.
            }
            catch (ShortLinkIdempotencyConflictException)
            {
                var existing = await idempotencyRepository!
                    .FindByIdempotencyKeyAsync(idempotencyKey!, cancellationToken);
                var replay = ResolveIdempotencyReplay(
                    existing,
                    originalUrl,
                    request.ExpiresAt.Value,
                    request.CreatedByUserId);
                return replay
                    ?? CreateShortLinkResult.Failure(
                        ShortLinkErrorCodes.IdempotencyConflict,
                        "The Idempotency-Key was already used for a different request.");
            }
        }

        return CreateShortLinkResult.Failure(
            ShortLinkErrorCodes.UnableToGenerateCode,
            "A unique short code could not be generated.");
    }

    private static CreateShortLinkResult? ResolveIdempotencyReplay(
        ShortLink? existing,
        Uri originalUrl,
        DateTimeOffset expiresAt,
        string? createdByUserId)
    {
        if (existing is null)
        {
            return null;
        }

        return string.Equals(existing.OriginalUrl.AbsoluteUri, originalUrl.AbsoluteUri, StringComparison.Ordinal)
            && existing.ExpiresAt == expiresAt
            && string.Equals(existing.CreatedByUserId, NormalizeIdentity(createdByUserId), StringComparison.Ordinal)
            ? CreateShortLinkResult.Replay(existing)
            : CreateShortLinkResult.Failure(
                ShortLinkErrorCodes.IdempotencyConflict,
                "The Idempotency-Key was already used for a different request.");
    }

    private static string? NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<ResolveShortLinkResult> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ResolveShortLinkResult.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var normalizedCode = code.Trim();
        var now = timeProvider.GetUtcNow();
        using var activity = diagnosticsEnabled
            ? ShortenLinkDiagnostics.StartRedirectActivity()
            : null;
        var shortLink = await cache.FindByCodeAsync(normalizedCode, cancellationToken);
        if (shortLink is not null)
        {
            var cachedResult = await ResolveCachedAsync(shortLink, now, cancellationToken);
            CompleteRedirectDiagnostics(activity, cacheHit: true, cachedResult.Succeeded);
            if (cachedResult.Succeeded && cachedResult.ShortLink is not null)
            {
                PublishEvent(ShortLinkEventTypes.Redirected, cachedResult.ShortLink, cancellationToken);
            }

            return cachedResult;
        }

        shortLink = await repository.FindByCodeAsync(normalizedCode, cancellationToken);
        if (shortLink is null)
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResult.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        if (!shortLink.IsActive)
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResult.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive.");
        }

        if (shortLink.IsExpired(now))
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResult.Failure(ShortLinkErrorCodes.Expired, "Short link has expired.");
        }

        await cache.SetAsync(shortLink, cancellationToken);
        CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: true);
        PublishEvent(ShortLinkEventTypes.Redirected, shortLink, cancellationToken);

        return ResolveShortLinkResult.Success(shortLink);
    }

    public async Task<ShortLinkDetailsResult> GetDetailsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ShortLinkDetailsResult.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var shortLink = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        return shortLink is null
            ? ShortLinkDetailsResult.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.")
            : ShortLinkDetailsResult.Success(shortLink);
    }

    public async Task<DeactivateShortLinkResult> DeactivateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return DeactivateShortLinkResult.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var shortLink = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (shortLink is null)
        {
            return DeactivateShortLinkResult.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        shortLink.Deactivate();
        await repository.UpdateAsync(shortLink, cancellationToken);
        await cache.RemoveAsync(shortLink.Code, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Deactivated, shortLink, cancellationToken);

        return DeactivateShortLinkResult.Success();
    }

    public async Task<DeactivateShortLinkResult> ActivateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return DeactivateShortLinkResult.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var shortLink = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (shortLink is null)
        {
            return DeactivateShortLinkResult.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        shortLink.Activate();
        await repository.UpdateAsync(shortLink, cancellationToken);
        await cache.RemoveAsync(shortLink.Code, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Activated, shortLink, cancellationToken);

        return DeactivateShortLinkResult.Success();
    }

    public async Task<ShortLinkDetailsResult> UpdateAsync(
        string code,
        UpdateShortLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ShortLinkDetailsResult.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        if (!ShortLinkUrlValidator.TryCreate(request.OriginalUrl, out var originalUrl))
        {
            return ShortLinkDetailsResult.Failure(
                ShortLinkErrorCodes.InvalidUrl,
                "Original URL must be an absolute HTTP or HTTPS URL.");
        }

        var now = timeProvider.GetUtcNow();
        if (request.ExpiresAt is null)
        {
            return ShortLinkDetailsResult.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration is required.");
        }

        if (request.ExpiresAt <= now)
        {
            return ShortLinkDetailsResult.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration must be in the future.");
        }

        var existing = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (existing is null)
        {
            return ShortLinkDetailsResult.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        var updated = new ShortLink(
            existing.Code,
            originalUrl,
            existing.CreatedAt,
            request.ExpiresAt.Value,
            existing.IsActive,
            existing.CreatedByUserId,
            existing.CreatedByDisplayName,
            existing.CreatedByUsername);

        await repository.UpdateAsync(updated, cancellationToken);
        await cache.RemoveAsync(updated.Code, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Updated, updated, cancellationToken);

        return ShortLinkDetailsResult.Success(updated);
    }

    public async Task<DeactivateShortLinkResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return DeactivateShortLinkResult.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var normalizedCode = code.Trim();
        if (!await repository.ExistsByCodeAsync(normalizedCode, cancellationToken))
        {
            return DeactivateShortLinkResult.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        await repository.DeleteAsync(normalizedCode, cancellationToken);
        await cache.RemoveAsync(normalizedCode, cancellationToken);
        PublishEvent(
            ShortLinkLifecycleEvent.ForCode(
                ShortLinkEventTypes.Deleted,
                normalizedCode,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return DeactivateShortLinkResult.Success();
    }

    private async Task<ResolveShortLinkResult> ResolveCachedAsync(
        ShortLink shortLink,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!shortLink.IsActive)
        {
            await cache.RemoveAsync(shortLink.Code, cancellationToken);
            return ResolveShortLinkResult.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive.");
        }

        if (shortLink.IsExpired(now))
        {
            await cache.RemoveAsync(shortLink.Code, cancellationToken);
            return ResolveShortLinkResult.Failure(ShortLinkErrorCodes.Expired, "Short link has expired.");
        }

        return ResolveShortLinkResult.Success(shortLink);
    }

    private static (string ErrorCode, string ErrorMessage)? ValidateCode(string code)
    {
        if (!ShortCodeValidator.IsValid(code?.Trim()))
        {
            return (
                ShortLinkErrorCodes.InvalidCode,
                "Code can contain only letters, numbers, underscores, and hyphens.");
        }

        return null;
    }

    private void PublishEvent(
        string eventType,
        ShortLink shortLink,
        CancellationToken cancellationToken) =>
        PublishEvent(
            ShortLinkLifecycleEvent.FromShortLink(
                eventType,
                shortLink,
                timeProvider.GetUtcNow()),
            cancellationToken);

    private void PublishEvent(
        ShortLinkLifecycleEvent @event,
        CancellationToken cancellationToken)
    {
        if (eventSink is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _ = eventSink.TryPublish(@event, cancellationToken);
        }
        catch
        {
            // Event delivery is opt-in and fail-open; it must not change the
            // outcome or latency contract of the short-link operation.
        }
    }

    private void CompleteRedirectDiagnostics(
        Activity? activity,
        bool cacheHit,
        bool succeeded)
    {
        if (!diagnosticsEnabled)
        {
            return;
        }

        if (activity is not null)
        {
            activity.SetTag(ShortenLinkDiagnostics.CacheHitTagName, cacheHit);
            activity.SetTag(ShortenLinkDiagnostics.OutcomeTagName, succeeded ? "success" : "failure");
        }

        ShortenLinkDiagnostics.RecordRedirect(cacheHit, succeeded);
    }
}
