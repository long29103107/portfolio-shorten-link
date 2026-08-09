using FluentValidation;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Bulk;

public static class ShortLinkBulkOperations
{
    public const string Activate = "activate";
    public const string Deactivate = "deactivate";
    public const string Delete = "delete";
    public const string Organize = "organize";

    public static bool IsSupported(string? operation) =>
        operation is not null
        && operation.Trim() is var normalized
        && (normalized.Equals(Activate, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(Deactivate, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(Delete, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(Organize, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string operation) => operation.Trim().ToLowerInvariant();
}

public static class ShortLinkBulkOperationLimits
{
    public const int MaxCodes = 100;
}

public sealed record ExecuteShortLinkBulkOperationCommand(
    IReadOnlyList<string>? Codes,
    string Operation,
    string? Folder = null,
    IReadOnlyList<string>? Tags = null) : IRequest<ShortLinkBulkOperationResponse>;

public sealed class ExecuteShortLinkBulkOperationCommandValidator
    : AbstractValidator<ExecuteShortLinkBulkOperationCommand>
{
    public ExecuteShortLinkBulkOperationCommandValidator()
    {
        RuleFor(request => request.Codes)
            .NotNull()
            .NotEmpty()
            .Must(static codes => codes is not null && codes.Count <= ShortLinkBulkOperationLimits.MaxCodes)
            .WithName("codes")
            .WithMessage($"Select between 1 and {ShortLinkBulkOperationLimits.MaxCodes} short links.")
            .WithErrorCode(ErrorCodes.InvalidRequest);

        RuleForEach(request => request.Codes)
            .NotEmpty()
            .WithName("codes")
            .WithMessage("Short-link codes cannot be empty.")
            .WithErrorCode(ErrorCodes.InvalidRequest);

        RuleFor(request => request.Codes)
            .Must(static codes => codes is null
                || codes.Select(static code => code.Trim()).Distinct(StringComparer.Ordinal).Count() == codes.Count)
            .WithName("codes")
            .WithMessage("Short-link codes must be unique.")
            .WithErrorCode(ErrorCodes.InvalidRequest);

        RuleFor(request => request.Operation)
            .Must(ShortLinkBulkOperations.IsSupported)
            .WithName("operation")
            .WithMessage("Operation must be activate, deactivate, delete, or organize.")
            .WithErrorCode(ErrorCodes.InvalidRequest);

        RuleFor(request => request.Folder)
            .Must(static folder => folder is null || folder.Trim().Length <= ShortLinkOrganization.MaxFolderLength)
            .When(static request => string.Equals(
                request.Operation?.Trim(),
                ShortLinkBulkOperations.Organize,
                StringComparison.OrdinalIgnoreCase))
            .WithName("folder")
            .WithMessage($"Folder must be at most {ShortLinkOrganization.MaxFolderLength} characters.")
            .WithErrorCode(ShortLinkErrorCodes.InvalidFolder);

        RuleFor(request => request.Tags)
            .Must(static tags => ShortLinkOrganization.TryNormalize(
                null,
                tags,
                out _,
                out _,
                out _,
                out _))
            .When(static request => string.Equals(
                request.Operation?.Trim(),
                ShortLinkBulkOperations.Organize,
                StringComparison.OrdinalIgnoreCase))
            .WithName("tags")
            .WithMessage("Tags must be valid, unique, and within the supported limits.")
            .WithErrorCode(ShortLinkErrorCodes.InvalidTags);

        RuleFor(request => request)
            .Must(static request => string.Equals(
                request.Operation?.Trim(),
                ShortLinkBulkOperations.Organize,
                StringComparison.OrdinalIgnoreCase)
                || (request.Folder is null && request.Tags is null))
            .WithName("operation")
            .WithMessage("Folder and tags are only supported by the organize operation.")
            .WithErrorCode(ErrorCodes.InvalidRequest);
    }
}

internal sealed class ExecuteShortLinkBulkOperationCommandHandler(
    IShortLinkService shortLinkService,
    IShortLinkShareRepository shareRepository,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<ExecuteShortLinkBulkOperationCommand, ShortLinkBulkOperationResponse>
{
    public async Task<ShortLinkBulkOperationResponse> Handle(
        ExecuteShortLinkBulkOperationCommand request,
        CancellationToken cancellationToken)
    {
        var operation = ShortLinkBulkOperations.Normalize(request.Operation);
        var permission = operation == ShortLinkBulkOperations.Delete
            ? ShortenLinkPermissionCatalog.ShortLinksDelete
            : operation == ShortLinkBulkOperations.Organize
                ? ShortenLinkPermissionCatalog.ShortLinksUpdate
                : ShortenLinkPermissionCatalog.ShortLinksStatus;
        var actor = await accessGuard.GetAuthorizedUserAsync(permission, cancellationToken);
        var items = new List<ShortLinkBulkOperationItemResponse>(request.Codes!.Count);

        foreach (var rawCode in request.Codes)
        {
            var code = rawCode.Trim();
            try
            {
                var existing = ShortLinkFeatureSupport.GetRequired(
                    await shortLinkService.GetDetailsAsync(code, cancellationToken));
                await accessGuard.EnsureAccessAsync(
                    existing,
                    actor,
                    ShortLinkShareAccess.Edit,
                    operation == ShortLinkBulkOperations.Delete,
                    operation == ShortLinkBulkOperations.Delete
                        ? "Only the owner or an admin can delete this short link."
                        : "Edit access is required for this short link.",
                    cancellationToken);

                switch (operation)
                {
                    case ShortLinkBulkOperations.Activate:
                        ShortLinkFeatureSupport.EnsureSucceeded(
                            await shortLinkService.ActivateAsync(code, cancellationToken));
                        await auditWriter.RecordAsync(
                            actor,
                            ShortLinkAuditActions.Activated,
                            existing.Code,
                            existing.CreatedByUserId,
                            cancellationToken: cancellationToken);
                        break;
                    case ShortLinkBulkOperations.Deactivate:
                        ShortLinkFeatureSupport.EnsureSucceeded(
                            await shortLinkService.DeactivateAsync(code, cancellationToken));
                        await auditWriter.RecordAsync(
                            actor,
                            ShortLinkAuditActions.Deactivated,
                            existing.Code,
                            existing.CreatedByUserId,
                            cancellationToken: cancellationToken);
                        break;
                    case ShortLinkBulkOperations.Delete:
                        ShortLinkFeatureSupport.EnsureSucceeded(
                            await shortLinkService.DeleteAsync(code, cancellationToken));
                        await shareRepository.DeleteByShortCodeAsync(code, cancellationToken);
                        await auditWriter.RecordAsync(
                            actor,
                            ShortLinkAuditActions.Deleted,
                            existing.Code,
                            existing.CreatedByUserId,
                            cancellationToken: cancellationToken);
                        break;
                    case ShortLinkBulkOperations.Organize:
                        ShortLinkFeatureSupport.GetRequired(
                            await shortLinkService.UpdateOrganizationAsync(
                                code,
                                request.Folder,
                                request.Tags,
                                cancellationToken));
                        await auditWriter.RecordAsync(
                            actor,
                            ShortLinkAuditActions.Updated,
                            existing.Code,
                            existing.CreatedByUserId,
                            detail: "bulk_organization",
                            cancellationToken: cancellationToken);
                        break;
                }

                items.Add(new ShortLinkBulkOperationItemResponse(code, true));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ShortenLinkException exception)
            {
                items.Add(new ShortLinkBulkOperationItemResponse(
                    code,
                    false,
                    exception.ErrorCode,
                    exception.Message));
            }
        }

        var succeededCount = items.Count(static item => item.Succeeded);
        return new ShortLinkBulkOperationResponse(
            operation,
            items.Count,
            succeededCount,
            items.Count - succeededCount,
            items);
    }
}
