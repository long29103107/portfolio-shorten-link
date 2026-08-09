using System.Diagnostics;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Services;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Events;
using ShortenLink.Core.Diagnostics;
using ShortenLink.Core.Security;
using ShortLinkDetailsResponse = ShortenLink.Core.Contracts.Responses.ShortLinkDetailsResponse;

namespace ShortenLink.Application.Services;

public sealed partial class ShortLinkService : IShortLinkService, ITenantAwareShortLinkService
{
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

        if (request.MaxClicks is <= 0)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidMaxClicks,
                "MaxClicks must be a positive integer.");
        }

        if (request.MaxClicks is not null && repository is not IShortLinkClickLimitRepository)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.ClickLimitNotSupported,
                "The configured persistence provider does not support click limits.");
        }

        if (!ShortLinkPassword.IsValid(request.Password))
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidPassword,
                $"Password must be non-empty and at most {ShortLinkPassword.MaxLength} characters.");
        }

        if (!ShortLinkOrganization.TryNormalize(
                request.Folder,
                request.Tags,
                out var normalizedFolder,
                out var normalizedTags,
                out var organizationErrorCode,
                out var organizationErrorMessage))
        {
            return CreateShortLinkResponse.Failure(
                organizationErrorCode!,
                organizationErrorMessage!);
        }

        var passwordHash = request.Password is null
            ? null
            : ShortenLinkSecurityCredentialHasher.HashPassword(request.Password);

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

        if (request.ActiveFrom is not null && request.ActiveFrom >= request.ExpiresAt)
        {
            return CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.InvalidActivationWindow,
                "Activation must be earlier than expiration.");
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
                request.ActiveFrom,
                request.MaxClicks,
                request.Password,
                normalizedFolder,
                normalizedTags,
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
                tenantId: tenantId,
                activeFrom: request.ActiveFrom,
                maxClicks: request.MaxClicks,
                passwordHash: passwordHash,
                folder: normalizedFolder,
                tags: normalizedTags);

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
                    request.ActiveFrom,
                    request.MaxClicks,
                    request.Password,
                    normalizedFolder,
                    normalizedTags,
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
        DateTimeOffset? activeFrom,
        int? maxClicks,
        string? password,
        string? folder,
        IReadOnlyList<string> tags,
        string? createdByUserId,
        string? tenantId)
    {
        if (existing is null)
        {
            return null;
        }

        return string.Equals(existing.OriginalUrl.AbsoluteUri, originalUrl.AbsoluteUri, StringComparison.Ordinal)
            && existing.ExpiresAt == expiresAt
            && existing.ActiveFrom == activeFrom
            && existing.MaxClicks == maxClicks
            && PasswordMatches(existing, password)
            && string.Equals(existing.Folder, folder, StringComparison.Ordinal)
            && existing.Tags.SequenceEqual(tags, StringComparer.OrdinalIgnoreCase)
            && string.Equals(existing.CreatedByUserId, NormalizeIdentity(createdByUserId), StringComparison.Ordinal)
            && string.Equals(existing.TenantId, tenantId, StringComparison.Ordinal)
            ? CreateShortLinkResponse.Replay(existing)
            : CreateShortLinkResponse.Failure(
                ShortLinkErrorCodes.IdempotencyConflict,
                "The Idempotency-Key was already used for a different request.");
    }

    private static bool PasswordMatches(ShortLink existing, string? password) =>
        password is null
            ? !existing.IsPasswordProtected
            : existing.IsPasswordProtected && existing.VerifyPassword(password);

    private static string? NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

        if (request.MaxClicks is <= 0)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidMaxClicks,
                "MaxClicks must be a positive integer.");
        }

        if (request.MaxClicks is not null && repository is not IShortLinkClickLimitRepository)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.ClickLimitNotSupported,
                "The configured persistence provider does not support click limits.");
        }

        if (request.Password is not null && !ShortLinkPassword.IsValid(request.Password))
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidPassword,
                $"Password must be non-empty and at most {ShortLinkPassword.MaxLength} characters.");
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

        if (request.ActiveFrom is not null && request.ActiveFrom >= request.ExpiresAt)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidActivationWindow,
                "Activation must be earlier than expiration.");
        }

        var existing = await repository.FindByCodeAsync(code.Trim(), cancellationToken);
        if (existing is null)
        {
            return ShortLinkDetailsResponse.Failure(ShortLinkErrorCodes.NotFound, "Short link was not found.");
        }

        var folder = request.Folder is null ? existing.Folder : request.Folder;
        var tags = request.Tags ?? existing.Tags;
        if (!ShortLinkOrganization.TryNormalize(
                folder,
                tags,
                out var normalizedFolder,
                out var normalizedTags,
                out var organizationErrorCode,
                out var organizationErrorMessage))
        {
            return ShortLinkDetailsResponse.Failure(
                organizationErrorCode!,
                organizationErrorMessage!);
        }

        if (request.MaxClicks is not null && request.MaxClicks < existing.ClickCount)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.InvalidMaxClicks,
                "MaxClicks cannot be lower than the current click count.");
        }

        var passwordHash = existing.PasswordHash;
        if (request.ClearPassword)
        {
            passwordHash = null;
        }
        else if (request.Password is not null)
        {
            passwordHash = ShortenLinkSecurityCredentialHasher.HashPassword(request.Password);
        }

        var updated = new ShortLink(
            existing.Code,
            originalUrl,
            existing.CreatedAt,
            request.ExpiresAt.Value,
            existing.IsActive
                && (request.MaxClicks is null || existing.ClickCount < request.MaxClicks.Value),
            existing.CreatedByUserId,
            existing.CreatedByDisplayName,
            existing.CreatedByUsername,
            technicalId: existing.Id,
            idempotencyKey: existing.IdempotencyKey,
            tenantId: existing.TenantId,
            sharingMode: existing.SharingMode,
            activeFrom: request.ActiveFrom,
            maxClicks: request.MaxClicks,
            clickCount: existing.ClickCount,
            passwordHash: passwordHash,
            folder: normalizedFolder,
            tags: normalizedTags);

        await repository.UpdateAsync(updated, cancellationToken);
        await RemoveCachedAsync(updated.Code, updated.TenantId, cancellationToken);
        PublishEvent(ShortLinkEventTypes.Updated, updated, cancellationToken);

        return ShortLinkDetailsResponse.Success(updated);
    }

    public async Task<ShortLinkDetailsResponse> UpdateOrganizationAsync(
        string code,
        string? folder,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateCode(code);
        if (validationFailure is not null)
        {
            return ShortLinkDetailsResponse.Failure(
                validationFailure.Value.ErrorCode,
                validationFailure.Value.ErrorMessage);
        }

        var normalizedCode = code.Trim();
        var existing = await repository.FindByCodeAsync(normalizedCode, cancellationToken);
        if (existing is null)
        {
            return ShortLinkDetailsResponse.Failure(
                ShortLinkErrorCodes.NotFound,
                "Short link was not found.");
        }

        if (!ShortLinkOrganization.TryNormalize(
                folder,
                tags,
                out var normalizedFolder,
                out var normalizedTags,
                out var organizationErrorCode,
                out var organizationErrorMessage))
        {
            return ShortLinkDetailsResponse.Failure(
                organizationErrorCode!,
                organizationErrorMessage!);
        }

        var updated = new ShortLink(
            existing.Code,
            existing.OriginalUrl,
            existing.CreatedAt,
            existing.ExpiresAt,
            existing.IsActive,
            existing.CreatedByUserId,
            existing.CreatedByDisplayName,
            existing.CreatedByUsername,
            technicalId: existing.Id,
            idempotencyKey: existing.IdempotencyKey,
            tenantId: existing.TenantId,
            sharingMode: existing.SharingMode,
            activeFrom: existing.ActiveFrom,
            maxClicks: existing.MaxClicks,
            clickCount: existing.ClickCount,
            passwordHash: existing.PasswordHash,
            folder: normalizedFolder,
            tags: normalizedTags);

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
}
