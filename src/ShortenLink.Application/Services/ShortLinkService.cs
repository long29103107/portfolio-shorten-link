using System.Diagnostics;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Services;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Events;
using ShortenLink.Core.Diagnostics;
using ShortLinkDetailsResponse = ShortenLink.Core.Contracts.Responses.ShortLinkDetailsResponse;

namespace ShortenLink.Application.Services;

public sealed class ShortLinkService : IShortLinkService, ITenantAwareShortLinkService
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
        CancellationToken cancellationToken = default)
    {
        accessScope = NormalizeTenantScope(accessScope);
        return repository.ListAccessibleRecentAsync(
            Math.Clamp(limit, 1, 500),
            beforeCreatedAt,
            beforeCode,
            accessScope,
            cancellationToken);
    }

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
        accessScope = NormalizeTenantScope(accessScope);
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

    public Task<ShortLinkListPage> ListAccessibleCursorPageAsync(
        int limit,
        string? search,
        ShortLinkListStatus status,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        DateTimeOffset beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        accessScope = NormalizeTenantScope(accessScope);
        var now = timeProvider.GetUtcNow();
        var query = new ShortLinkListQuery(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            status,
            sortBy,
            sortDirection,
            now,
            now.AddDays(7),
            accessScope,
            beforeCreatedAt,
            string.IsNullOrWhiteSpace(beforeCode) ? null : beforeCode.Trim());
        return repository.ListPageAsync(
            0,
            Math.Clamp(limit, 1, 501),
            query,
            cancellationToken);
    }

    public async Task<CreateShortLinkResponse> CreateAsync(
        CreateShortLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ShortLinkUrlValidator.TryCreate(request.OriginalUrl, out var originalUrl))
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidUrl,
                "Original URL must be an absolute HTTP or HTTPS URL.");
        }

        if (!ShortLinkIdempotencyKey.IsValid(request.IdempotencyKey))
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidIdempotencyKey,
                $"Idempotency-Key must be at most {ShortLinkIdempotencyKey.MaxLength} characters.");
        }

        var idempotencyKey = ShortLinkIdempotencyKey.Normalize(request.IdempotencyKey);
        if (!ShortLinkTenantId.IsValid(request.TenantId))
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidTenantId,
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.");
        }

        var tenantId = ShortLinkTenantId.Normalize(request.TenantId);
        var tenantRepository = repository as IShortLinkTenantRepository;
        if (tenantId is not null && tenantRepository is null)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.TenantNotSupported,
                "The configured persistence provider does not support tenant partitions.");
        }

        var idempotencyRepository = repository as IShortLinkIdempotencyRepository;
        if (idempotencyKey is not null && idempotencyRepository is null)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.IdempotencyNotSupported,
                "The configured persistence provider does not support idempotent creates.");
        }

        var now = timeProvider.GetUtcNow();
        if (request.ExpiresAt is null)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration is required.");
        }

        if (request.ExpiresAt <= now)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration must be in the future.");
        }

        if (idempotencyKey is not null)
        {
            var existing = tenantId is null
                ? await idempotencyRepository!
                    .FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
                : await tenantRepository!
                    .FindByTenantIdempotencyKeyAsync(tenantId, idempotencyKey, cancellationToken);
            var replay = ResolveIdempotencyReplay(
                existing,
                originalUrl,
                request.ExpiresAt.Value,
                request.CreatedByUserId,
                tenantId);
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
                idempotencyKey: idempotencyKey,
                tenantId: tenantId);

            try
            {
                await repository.AddAsync(shortLink, cancellationToken);
                PublishEvent(ShortLinkEventTypes.Created, shortLink, cancellationToken);
                return CreateShortLinkResponse.Success(shortLink);
            }
            catch (ShortLinkCodeConflictException)
            {
                // Another writer won the race after ExistsByCodeAsync. Generate
                // a fresh candidate while preserving unrelated persistence errors.
            }
            catch (ShortLinkIdempotencyConflictException)
            {
                var existing = tenantId is null
                    ? await idempotencyRepository!
                        .FindByIdempotencyKeyAsync(idempotencyKey!, cancellationToken)
                    : await tenantRepository!
                        .FindByTenantIdempotencyKeyAsync(tenantId, idempotencyKey!, cancellationToken);
                var replay = ResolveIdempotencyReplay(
                    existing,
                    originalUrl,
                    request.ExpiresAt.Value,
                    request.CreatedByUserId,
                    tenantId);
                return replay
                    ?? CreateShortLinkResponse.Failure(
                        ShortLinkErrorCodes.IdempotencyConflict,
                        "The Idempotency-Key was already used for a different request.");
            }
        }

        return CreateShortLinkResponse.Failure(
            ShortLinkErrorCodes.UnableToGenerateCode,
            "A unique short code could not be generated.");
    }

    private static CreateShortLinkResponse? ResolveIdempotencyReplay(
        ShortLink? existing,
        Uri originalUrl,
        DateTimeOffset expiresAt,
        string? createdByUserId,
        string? tenantId)
    {
        if (existing is null)
        {
            return null;
        }

        return string.Equals(existing.OriginalUrl.AbsoluteUri, originalUrl.AbsoluteUri, StringComparison.Ordinal)
            && existing.ExpiresAt == expiresAt
            && string.Equals(existing.CreatedByUserId, NormalizeIdentity(createdByUserId), StringComparison.Ordinal)
            && string.Equals(existing.TenantId, tenantId, StringComparison.Ordinal)
            ? CreateShortLinkResponse.Replay(existing)
            : CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.IdempotencyConflict,
                "The Idempotency-Key was already used for a different request.");
    }

    private static string? NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public Task<ResolveShortLinkResponse> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(code, cancellationToken, tenantId: null);

    public async Task<ResolveShortLinkResponse> ResolveAsync(
        string code,
        CancellationToken cancellationToken,
        string? tenantId)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ResolveShortLinkResponse.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var normalizedCode = code.Trim();
        if (!ShortLinkTenantId.IsValid(tenantId))
        {
            return ResolveShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidTenantId,
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.");
        }

        tenantId = ShortLinkTenantId.Normalize(tenantId);
        var now = timeProvider.GetUtcNow();
        using var activity = diagnosticsEnabled
            ? ShortenLinkDiagnostics.StartRedirectActivity()
            : null;
        var shortLink = await FindCachedAsync(normalizedCode, tenantId, cancellationToken);
        if (shortLink is not null)
        {
            if (!TenantMatches(shortLink, tenantId))
            {
                await RemoveCachedAsync(shortLink.Code, tenantId, cancellationToken);
                shortLink = null;
            }
        }

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

        shortLink = cache is IShortLinkCacheLoader cacheLoader
            ? tenantId is null
                ? await cacheLoader.GetOrCreateAsync(
                    normalizedCode,
                    token => repository.FindByCodeAsync(normalizedCode, token),
                    cancellationToken)
                : await cacheLoader.GetOrCreateAsync(
                    normalizedCode,
                    tenantId,
                    token => repository.FindByCodeAsync(normalizedCode, token),
                    cancellationToken)
            : await repository.FindByCodeAsync(normalizedCode, cancellationToken);
        if (shortLink is null || !TenantMatches(shortLink, tenantId))
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        if (!shortLink.IsActive)
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive.");
        }

        if (shortLink.IsExpired(now))
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Expired, "Short link has expired.");
        }

        await SetCachedAsync(shortLink, tenantId, cancellationToken);
        CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: true);
        PublishEvent(ShortLinkEventTypes.Redirected, shortLink, cancellationToken);

        return ResolveShortLinkResponse.Success(shortLink);
    }

    public async Task<ShortLinkDetailsResponse> GetDetailsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ShortLinkDetailsResponse.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var shortLink = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        return shortLink is null
            ? ShortLinkDetailsResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.")
            : ShortLinkDetailsResponse.Success(shortLink);
    }

    public async Task<DeactivateShortLinkResponse> DeactivateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return DeactivateShortLinkResponse.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var shortLink = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (shortLink is null)
        {
            return DeactivateShortLinkResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        shortLink.Deactivate();
        await repository.UpdateAsync(shortLink, cancellationToken);
        await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Deactivated, shortLink, cancellationToken);

        return DeactivateShortLinkResponse.Success();
    }

    public async Task<DeactivateShortLinkResponse> ActivateAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return DeactivateShortLinkResponse.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var shortLink = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (shortLink is null)
        {
            return DeactivateShortLinkResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        shortLink.Activate();
        await repository.UpdateAsync(shortLink, cancellationToken);
        await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Activated, shortLink, cancellationToken);

        return DeactivateShortLinkResponse.Success();
    }

    public async Task<ShortLinkDetailsResponse> UpdateAsync(
        string code,
        UpdateShortLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ShortLinkDetailsResponse.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        if (!ShortLinkUrlValidator.TryCreate(request.OriginalUrl, out var originalUrl))
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidUrl,
                "Original URL must be an absolute HTTP or HTTPS URL.");
        }

        var now = timeProvider.GetUtcNow();
        if (request.ExpiresAt is null)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration is required.");
        }

        if (request.ExpiresAt <= now)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidExpiration,
                "Expiration must be in the future.");
        }

        var existing = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (existing is null)
        {
            return ShortLinkDetailsResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        var updated = new ShortLink(
            existing.Code,
            originalUrl,
            existing.CreatedAt,
            request.ExpiresAt.Value,
            existing.IsActive,
            existing.CreatedByUserId,
            existing.CreatedByDisplayName,
            existing.CreatedByUsername,
            technicalId: existing.Id,
            idempotencyKey: existing.IdempotencyKey,
            tenantId: existing.TenantId,
            sharingMode: existing.SharingMode);

        await repository.UpdateAsync(updated, cancellationToken);
        await RemoveCachedAsync(updated.Code, updated.TenantId, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Updated, updated, cancellationToken);

        return ShortLinkDetailsResponse.Success(updated);
    }

    public async Task<DeactivateShortLinkResponse> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return DeactivateShortLinkResponse.Failure(validationFailure.Value.ErrorCode, validationFailure.Value.ErrorMessage);
        }

        var normalizedCode = code.Trim();
        var existing = await repository.FindByCodeAsync(normalizedCode, cancellationToken);
        if (existing is null)
        {
            return DeactivateShortLinkResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        await repository.DeleteAsync(normalizedCode, cancellationToken);
        await RemoveCachedAsync(normalizedCode, existing.TenantId, cancellationToken);
        PublishEvent(
            ShortLinkLifecycleEvent.ForCode(
                ShortLinkEventTypes.Deleted,
                normalizedCode,
                timeProvider.GetUtcNow(),
                existing.TenantId),
            cancellationToken);

        return DeactivateShortLinkResponse.Success();
    }

    private async Task<ResolveShortLinkResponse> ResolveCachedAsync(
        ShortLink shortLink,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!shortLink.IsActive)
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive.");
        }

        if (shortLink.IsExpired(now))
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Expired, "Short link has expired.");
        }

        return ResolveShortLinkResponse.Success(shortLink);
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

    private async Task<ShortLink?> FindCachedAsync(
        string code,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            return await cache.FindByCodeAsync(code, cancellationToken);
        }

        return cache is ITenantAwareShortLinkCache tenantCache
            ? await tenantCache.FindByCodeAsync(code, tenantId, cancellationToken)
            : null;
    }

    private Task SetCachedAsync(
        ShortLink shortLink,
        string? tenantId,
        CancellationToken cancellationToken) =>
        tenantId is null || cache is ITenantAwareShortLinkCache
            ? cache.SetAsync(shortLink, cancellationToken)
            : Task.CompletedTask;

    private Task RemoveCachedAsync(
        string code,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId is not null)
        {
            return cache is ITenantAwareShortLinkCache tenantCache
                ? tenantCache.RemoveAsync(code, tenantId, cancellationToken)
                : Task.CompletedTask;
        }

        return cache.RemoveAsync(code, cancellationToken);
    }

    private static bool TenantMatches(ShortLink shortLink, string? tenantId) =>
        string.Equals(shortLink.TenantId, tenantId, StringComparison.Ordinal);

    private ShortLinkAccessScope NormalizeTenantScope(ShortLinkAccessScope accessScope)
    {
        ArgumentNullException.ThrowIfNull(accessScope);
        if (!ShortLinkTenantId.IsValid(accessScope.TenantId))
        {
            throw new RequestValidationException(
                ShortLinkErrorCodes.InvalidTenantId,
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.");
        }

        var tenantId = ShortLinkTenantId.Normalize(accessScope.TenantId);
        if (tenantId is not null && repository is not IShortLinkTenantRepository)
        {
            throw new BusinessRuleException(
                ShortLinkErrorCodes.TenantNotSupported,
                "The configured persistence provider does not support tenant partitions.");
        }

        return accessScope with { TenantId = tenantId };
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
