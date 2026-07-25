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
    DateTimeOffset? ExpiresAt) : IRequest<ShortLink>;

internal sealed class CreateShortLinkCommandHandler(
    IShortLinkService shortLinkService,
    ICurrentRequestContext requestContext,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<CreateShortLinkCommand, ShortLink>
{
    public async Task<ShortLink> Handle(
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
                creator?.Username),
            cancellationToken);

        if (!result.Succeeded || result.ShortLink is null)
        {
            throw CreateException(
                result.ErrorCode ?? ErrorCodes.CreateFailed,
                result.ErrorMessage ?? "The short link could not be created.");
        }

        await auditWriter.RecordAsync(
            actor,
            ShortLinkAuditActions.Created,
            result.ShortLink.Code,
            result.ShortLink.CreatedByUserId,
            cancellationToken: cancellationToken);

        return result.ShortLink;
    }

    private static ShortenLinkException CreateException(string errorCode, string message) =>
        errorCode switch
        {
            ShortLinkErrorCodes.InvalidUrl or ShortLinkErrorCodes.InvalidExpiration =>
                new RequestValidationException(errorCode, message),
            ShortLinkErrorCodes.NotFound => new NotFoundException(errorCode, message),
            ShortLinkErrorCodes.Expired or ShortLinkErrorCodes.Inactive =>
                new ResourceGoneException(errorCode, message),
            ErrorCodes.DuplicateAlias => new ConflictException(errorCode, message),
            _ => new BusinessRuleException(errorCode, message)
        };
}
