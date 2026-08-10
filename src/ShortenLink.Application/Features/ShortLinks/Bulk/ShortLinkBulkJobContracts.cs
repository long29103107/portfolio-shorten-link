using FluentValidation;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Bulk;

public static class ShortLinkBulkJobStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class ShortLinkBulkJobLimits
{
    public const int MaxCodes = 1000;
}

public sealed record ShortLinkBulkJobAcceptedResponse(
    Guid JobId,
    string Status,
    int TotalCount);

public sealed record ShortLinkBulkJobStatusResponse(
    Guid JobId,
    string Status,
    int TotalCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    ShortLinkBulkOperationResponse? Result = null,
    string? Error = null);

public interface IShortLinkBulkJobScheduler
{
    Task<ShortLinkBulkJobAcceptedResponse> EnqueueAsync(
        ExecuteShortLinkBulkOperationCommand request,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null);

    Task<ShortLinkBulkJobStatusResponse> GetStatusAsync(
        Guid jobId,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default);

    Task<ShortLinkBulkJobStatusResponse> CancelAsync(
        Guid jobId,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default);
}

public sealed record CreateShortLinkBulkJobCommand(
    IReadOnlyList<string>? Codes,
    string Operation,
    string? Folder = null,
    IReadOnlyList<string>? Tags = null,
    string? IdempotencyKey = null) : IRequest<ShortLinkBulkJobAcceptedResponse>, IBypassUnitOfWork;

public sealed class CreateShortLinkBulkJobCommandValidator
    : AbstractValidator<CreateShortLinkBulkJobCommand>
{
    public CreateShortLinkBulkJobCommandValidator()
    {
        RuleFor(request => request.Codes)
            .NotNull().NotEmpty()
            .Must(static codes => codes is not null && codes.Count <= ShortLinkBulkJobLimits.MaxCodes)
            .WithMessage($"Select between 1 and {ShortLinkBulkJobLimits.MaxCodes} short links.")
            .WithErrorCode(ErrorCodes.InvalidRequest);
        RuleForEach(request => request.Codes).NotEmpty().WithErrorCode(ErrorCodes.InvalidRequest);
        RuleFor(request => request.Codes)
            .Must(static codes => codes is null || codes.Select(static code => code.Trim()).Distinct(StringComparer.Ordinal).Count() == codes.Count)
            .WithMessage("Short-link codes must be unique.")
            .WithErrorCode(ErrorCodes.InvalidRequest);
        RuleFor(request => request.Operation)
            .Must(ShortLinkBulkOperations.IsSupported)
            .WithMessage("Operation must be activate, deactivate, delete, or organize.")
            .WithErrorCode(ErrorCodes.InvalidRequest);
        RuleFor(request => request.Folder)
            .Must(static folder => folder is null || folder.Trim().Length <= ShortLinkOrganization.MaxFolderLength)
            .When(static request => string.Equals(request.Operation?.Trim(), ShortLinkBulkOperations.Organize, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode(ShortLinkErrorCodes.InvalidFolder);
        RuleFor(request => request.Tags)
            .Must(static tags => ShortLinkOrganization.TryNormalize(null, tags, out _, out _, out _, out _))
            .When(static request => string.Equals(request.Operation?.Trim(), ShortLinkBulkOperations.Organize, StringComparison.OrdinalIgnoreCase))
            .WithErrorCode(ShortLinkErrorCodes.InvalidTags);
        RuleFor(request => request)
            .Must(static request => string.Equals(request.Operation?.Trim(), ShortLinkBulkOperations.Organize, StringComparison.OrdinalIgnoreCase)
                || (request.Folder is null && request.Tags is null))
            .WithMessage("Folder and tags are only supported by the organize operation.")
            .WithErrorCode(ErrorCodes.InvalidRequest);
        RuleFor(request => request.IdempotencyKey)
            .Must(static key => key is null || key.Trim().Length <= 256)
            .WithMessage("Idempotency key must be at most 256 characters.")
            .WithErrorCode(ErrorCodes.InvalidRequest);
    }
}

internal sealed class CreateShortLinkBulkJobCommandHandler(
    IShortLinkBulkJobScheduler scheduler,
    ICurrentRequestContext requestContext)
    : IRequestHandler<CreateShortLinkBulkJobCommand, ShortLinkBulkJobAcceptedResponse>
{
    public async Task<ShortLinkBulkJobAcceptedResponse> Handle(CreateShortLinkBulkJobCommand request, CancellationToken cancellationToken)
    {
        var operation = ShortLinkBulkOperations.Normalize(request.Operation);
        var permission = operation == ShortLinkBulkOperations.Delete
            ? ShortenLinkPermissionCatalog.ShortLinksDelete
            : operation == ShortLinkBulkOperations.Organize
                ? ShortenLinkPermissionCatalog.ShortLinksUpdate
                : ShortenLinkPermissionCatalog.ShortLinksStatus;
        var actor = await requestContext.AuthorizeAsync(permission, cancellationToken);
        return await scheduler.EnqueueAsync(
            new ExecuteShortLinkBulkOperationCommand(request.Codes, request.Operation, request.Folder, request.Tags),
            actor,
            cancellationToken,
            request.IdempotencyKey);
    }
}

public sealed record GetShortLinkBulkJobStatusQuery(Guid JobId) : IRequest<ShortLinkBulkJobStatusResponse>;

public sealed record CancelShortLinkBulkJobCommand(Guid JobId) : IRequest<ShortLinkBulkJobStatusResponse>;

internal sealed class GetShortLinkBulkJobStatusQueryHandler(
    IShortLinkBulkJobScheduler scheduler,
    ICurrentRequestContext requestContext)
    : IRequestHandler<GetShortLinkBulkJobStatusQuery, ShortLinkBulkJobStatusResponse>
{
    public async Task<ShortLinkBulkJobStatusResponse> Handle(GetShortLinkBulkJobStatusQuery request, CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(ShortenLinkPermissionCatalog.ShortLinksRead, cancellationToken);
        return await scheduler.GetStatusAsync(request.JobId, actor, cancellationToken);
    }
}

internal sealed class CancelShortLinkBulkJobCommandHandler(
    IShortLinkBulkJobScheduler scheduler,
    ICurrentRequestContext requestContext)
    : IRequestHandler<CancelShortLinkBulkJobCommand, ShortLinkBulkJobStatusResponse>
{
    public async Task<ShortLinkBulkJobStatusResponse> Handle(CancelShortLinkBulkJobCommand request, CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(ShortenLinkPermissionCatalog.ShortLinksRead, cancellationToken);
        return await scheduler.CancelAsync(request.JobId, actor, cancellationToken);
    }
}
