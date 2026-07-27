using System.Globalization;
using System.Text;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Audit;

public sealed record ListShortLinkAuditEventsQuery(
    int? Limit,
    string? Cursor,
    string? Action,
    string? TargetId,
    string? ActorId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<ShortLinkAuditEventsResponse>;

internal sealed class ListShortLinkAuditEventsQueryHandler(
    IShortLinkAuditRepository auditRepository,
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

        if (request.From is not null && request.To is not null && request.From > request.To)
        {
            throw new RequestValidationException(
                ErrorCodes.InvalidFilter,
                "The audit time range is invalid.");
        }

        if (!TryDecodeCursor(request.Cursor, out var beforeOccurredAt, out var beforeId))
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
        var limit = Math.Clamp(request.Limit ?? 50, 1, 200);
        var page = await auditRepository.ListAsync(
            new ShortLinkAuditQuery(
                limit,
                beforeOccurredAt,
                beforeId,
                Normalize(request.Action),
                Normalize(request.TargetId),
                Normalize(request.ActorId),
                request.From,
                request.To,
                new ShortLinkAuditAccessScope(actor.UserId, actor.IsAdmin, sharedCodes)),
            cancellationToken);

        var items = page.Items
            .Select(ShortLinkAuditEventResponse.FromDomain)
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
