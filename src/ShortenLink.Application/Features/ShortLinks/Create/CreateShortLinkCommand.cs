using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Create;

public sealed record CreateShortLinkCommand(
    string OriginalUrl,
    DateTimeOffset? ExpiresAt,
    string? IdempotencyKey = null,
    DateTimeOffset? ActiveFromUtc = null,
    int? MaxClicks = null) : IRequest<CreateShortLinkResponse>;

internal sealed class CreateShortLinkCommandHandler(
    IShortLinkService shortLinkService,
    ICurrentRequestContext requestContext,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<CreateShortLinkCommand, CreateShortLinkResponse>
{
    public async Task<CreateShortLinkResponse> Handle(
        CreateShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext
            .AuthorizeAsync(ShortenLinkPermissionCatalog.ShortLinksCreate, cancellationToken)
            ;
        var creator = await requestContext
            .GetCurrentUserAsync(cancellationToken)
            ;

        var result = await shortLinkService.CreateAsync(
            new CreateShortLinkRequest(
                request.OriginalUrl,
                request.ExpiresAt,
                creator?.UserId,
                creator?.DisplayName,
                creator?.Username,
                request.IdempotencyKey,
                actor.TenantId,
                request.ActiveFromUtc,
                request.MaxClicks),
            cancellationToken);

        if (!result.Succeeded || result.ShortLink is null)
        {
            throw CreateException(
                result.ErrorCode ?? ErrorCodes.CreateFailed,
                result.ErrorMessage ?? "The short link could not be created.");
        }

        if (!result.Replayed)
        {
            await auditWriter.RecordAsync(
                actor,
                ShortLinkAuditActions.Created,
                result.ShortLink.Code,
                result.ShortLink.CreatedByUserId,
                cancellationToken: cancellationToken);
        }

        return result;
    }

    private static ShortenLinkException CreateException(string errorCode, string message) =>
        errorCode switch
        {
            ShortLinkErrorCodes.InvalidUrl
                or ShortLinkErrorCodes.InvalidExpiration
                or ShortLinkErrorCodes.InvalidActivationWindow
                or ShortLinkErrorCodes.InvalidMaxClicks
                or ShortLinkErrorCodes.InvalidIdempotencyKey
                or ShortLinkErrorCodes.InvalidTenantId =>
                new RequestValidationException(errorCode, message),
            ShortLinkErrorCodes.IdempotencyConflict => new ConflictException(errorCode, message),
            ShortLinkErrorCodes.NotFound => new NotFoundException(errorCode, message),
            ShortLinkErrorCodes.Expired
                or ShortLinkErrorCodes.Inactive
                or ShortLinkErrorCodes.ClickLimitReached =>
                new ResourceGoneException(errorCode, message),
            ErrorCodes.DuplicateAlias => new ConflictException(errorCode, message),
            _ => new BusinessRuleException(errorCode, message)
        };
}
