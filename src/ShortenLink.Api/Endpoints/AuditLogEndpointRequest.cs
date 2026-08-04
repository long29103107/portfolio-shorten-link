using ShortenLink.Core.Contracts.Requests.BaseRequest;

namespace ShortenLink.Api.Endpoints;

internal sealed class AuditLogEndpointRequest : ListRequest
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public int? Limit { get; set; }

    public string? Cursor { get; set; }

    public string? Action { get; set; }

    public string? TargetId { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }
}
