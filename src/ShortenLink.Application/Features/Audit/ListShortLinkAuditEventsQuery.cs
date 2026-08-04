using System.Globalization;
using System.Text;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;
using ShortenLink.Auditing;

namespace ShortenLink.Application.Features.Audit;

public sealed record ListShortLinkAuditEventsQuery(AuditLogEndpointRequest Params) : IRequest<ShortLinkAuditEventsResponse>;

public sealed record ListShortLinkAuditActionsQuery : IRequest<ShortLinkAuditActionsResponse>;

internal sealed class ListShortLinkAuditActionsQueryHandler(
    IAuditRepository auditRepository,
    IShortLinkShareRepository shareRepository,
    ICurrentRequestContext requestContext)
    : IRequestHandler<ListShortLinkAuditActionsQuery, ShortLinkAuditActionsResponse>
{
    public async Task<ShortLinkAuditActionsResponse> Handle(
        ListShortLinkAuditActionsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            ShortenLinkPermissionCatalog.AuditLogsRead,
            cancellationToken);
        var sharedCodes = actor.IsAdmin || string.IsNullOrWhiteSpace(actor.UserId)
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await shareRepository.ListSharedAccessAsync(actor.UserId, cancellationToken))
                .Keys
                .ToHashSet(StringComparer.Ordinal);
        var actions = await auditRepository.ListActionsAsync(
            new AuditReadScope(actor.UserId, actor.IsAdmin, sharedCodes),
            cancellationToken);
        return new ShortLinkAuditActionsResponse(actions);
    }
}

internal sealed class ListShortLinkAuditEventsQueryHandler(
    IAuditRepository auditRepository,
    IShortLinkShareRepository shareRepository,
    ICurrentRequestContext requestContext)
    : IRequestHandler<ListShortLinkAuditEventsQuery, ShortLinkAuditEventsResponse>
{
    public async Task<ShortLinkAuditEventsResponse> Handle(
        ListShortLinkAuditEventsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            ShortenLinkPermissionCatalog.AuditLogsRead,
            cancellationToken);

        if (!TryDecodeCursor(request.Params.Cursor, out var beforeOccurredAt, out var beforeId))
        {
            throw new RequestValidationException(
                ErrorCodes.InvalidCursor,
                "The audit cursor is invalid.");
        }

        var sharedCodes = actor.IsAdmin || string.IsNullOrWhiteSpace(actor.UserId)
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await shareRepository.ListSharedAccessAsync(actor.UserId, cancellationToken))
                .Keys
                .ToHashSet(StringComparer.Ordinal);
        var limit = Math.Clamp(request.Params.Limit ?? 50, 1, 200);
        var page = await auditRepository.ListAsync(
            new AuditQuery(
                limit,
                beforeOccurredAt,
                beforeId,
                Normalize(request.Params.Action),
                Normalize(request.Params.TargetId),
                Normalize(request.Params.ActorId),
                request.Params.From,
                request.Params.To,
                new AuditReadScope(actor.UserId, actor.IsAdmin, sharedCodes)),
            cancellationToken);

        var items = page.Items
            .Select(static auditEvent =>
                ShortLinkAuditEventResponse.FromDomain(auditEvent))
            .ToList();
        var nextCursor = items.Count == limit
            ? EncodeCursor(items[^1].OccurredAtUtc, items[^1].Id)
            : null;

        return new ShortLinkAuditEventsResponse(items, nextCursor);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryDecodeCursor(
        string? cursor,
        out DateTimeOffset? occurredAt,
        out Guid? id)
    {
        occurredAt = null;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var parts = Encoding.UTF8
                .GetString(Convert.FromBase64String(cursor))
                .Split('|', 2);
            if (parts.Length == 2
                && DateTimeOffset.TryParseExact(
                    parts[0],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedOccurredAt)
                && Guid.TryParse(parts[1], out var parsedId))
            {
                occurredAt = parsedOccurredAt;
                id = parsedId;
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }

    private static string EncodeCursor(DateTimeOffset occurredAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{occurredAt.ToString("O", CultureInfo.InvariantCulture)}|{id:D}"));
}
