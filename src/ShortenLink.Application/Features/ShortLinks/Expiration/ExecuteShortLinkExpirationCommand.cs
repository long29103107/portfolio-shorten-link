using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Security;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Services;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Expiration;

public sealed record ExecuteShortLinkExpirationCommand(
    DateTimeOffset? EvaluatedAtUtc,
    int? Limit,
    double? RetainExpiredForSeconds,
    bool? ResumeFromCheckpoint) : IRequest<ShortLinkExpirationExecutionResult>;

internal sealed class ExecuteShortLinkExpirationCommandHandler(
    IShortLinkExpirationExecutionService executionService,
    ShortLinkAccessGuard accessGuard)
    : IRequestHandler<ExecuteShortLinkExpirationCommand, ShortLinkExpirationExecutionResult>
{
    public async Task<ShortLinkExpirationExecutionResult> Handle(
        ExecuteShortLinkExpirationCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksStatus,
            cancellationToken);
        var retentionPolicy = request.RetainExpiredForSeconds is null
            ? null
            : new ShortLinkRetentionPolicy(TimeSpan.FromSeconds(request.RetainExpiredForSeconds.Value));
        return await executionService.ExecuteBatchAsync(
            new ShortLinkExpirationExecutionRequest(
                request.EvaluatedAtUtc ?? DateTimeOffset.UtcNow,
                actor.TenantId,
                request.Limit ?? ShortLinkExpirationEvaluator.DefaultLimit,
                retentionPolicy,
                request.ResumeFromCheckpoint ?? true),
            cancellationToken);
    }
}
