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

public sealed partial class ShortLinkService : IShortLinkService, ITenantAwareShortLinkService
{
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
            if (cachedResult.Succeeded && cachedResult.ShortLink is not null)
            {
                cachedResult = await ConsumeClickBudgetAsync(
                    cachedResult.ShortLink,
                    now,
                    cancellationToken);
            }
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

        if (shortLink.IsClickLimitReached)
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(
                ShortLinkErrorCodes.ClickLimitReached,
                "Short link click limit has been reached.");
        }

        if (!shortLink.IsActive)
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive.");
        }

        if (shortLink.IsScheduled(now))
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Scheduled, "Short link is not active yet.");
        }

        if (shortLink.IsExpired(now))
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Expired, "Short link has expired.");
        }

        var budgetResult = await ConsumeClickBudgetAsync(shortLink, now, cancellationToken);
        if (!budgetResult.Succeeded || budgetResult.ShortLink is null)
        {
            CompleteRedirectDiagnostics(activity, cacheHit: false, succeeded: false);
            return budgetResult;
        }

        if (shortLink.MaxClicks is null)
        {
            await SetCachedAsync(shortLink, tenantId, cancellationToken);
        }
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

    private async Task<ResolveShortLinkResponse> ResolveCachedAsync(
        ShortLink shortLink,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (shortLink.IsClickLimitReached)
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Failure(
                ShortLinkErrorCodes.ClickLimitReached,
                "Short link click limit has been reached.");
        }

        if (!shortLink.IsActive)
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive.");
        }

        if (shortLink.IsScheduled(now))
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Scheduled, "Short link is not active yet.");
        }

        if (shortLink.IsExpired(now))
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Expired, "Short link has expired.");
        }

        return ResolveShortLinkResponse.Success(shortLink);
    }

    private async Task<ResolveShortLinkResponse> ConsumeClickBudgetAsync(
        ShortLink shortLink,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (shortLink.MaxClicks is null)
        {
            return ResolveShortLinkResponse.Success(shortLink);
        }

        if (repository is not IShortLinkClickLimitRepository limitRepository)
        {
            return ResolveShortLinkResponse.Failure(
                ShortLinkErrorCodes.ClickLimitNotSupported,
                "The configured persistence provider does not support click limits.");
        }

        var result = await limitRepository.TryConsumeClickAsync(
            shortLink.Code,
            shortLink.TenantId,
            now,
            cancellationToken);
        if (result is ShortLinkClickConsumptionResult.Consumed
            or ShortLinkClickConsumptionResult.NotLimited)
        {
            await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            return ResolveShortLinkResponse.Success(shortLink);
        }

        await RemoveCachedAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
        return result switch
        {
            ShortLinkClickConsumptionResult.NotFound =>
                ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found."),
            ShortLinkClickConsumptionResult.Inactive =>
                ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Inactive, "Short link is inactive."),
            ShortLinkClickConsumptionResult.Scheduled =>
                ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Scheduled, "Short link is not active yet."),
            ShortLinkClickConsumptionResult.Expired =>
                ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.Expired, "Short link has expired."),
            ShortLinkClickConsumptionResult.LimitReached =>
                ResolveShortLinkResponse.Failure(ShortLinkErrorCodes.ClickLimitReached, "Short link click limit has been reached."),
            _ => ResolveShortLinkResponse.Failure(
                ShortLinkErrorCodes.ClickLimitNotSupported,
                "The configured persistence provider does not support click limits.")
        };
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
