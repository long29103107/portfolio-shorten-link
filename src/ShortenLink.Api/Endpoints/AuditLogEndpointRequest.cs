using ShortenLink.Core.Contracts.Requests;

namespace ShortenLink.Api.Endpoints;

internal sealed class AuditLogEndpointRequest : PagingListRequest
{
    public int? Limit { get; set; }

    public string? Cursor { get; set; }

    public string? Action { get; set; }

    public string? TargetId { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }
}
