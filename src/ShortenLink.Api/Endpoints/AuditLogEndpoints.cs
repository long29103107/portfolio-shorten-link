using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/audit-logs",
                static (
                    [AsParameters] AuditLogEndpointRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                    sender.Send(
                        new ListShortLinkAuditEventsQuery(request),
                        cancellationToken))
            .WithTags("Audit Logs")
            .WithName("ListAuditLogs");

        endpoints.MapGet(
                "/api/audit-logs/actions",
                static (ISender sender, CancellationToken cancellationToken) =>
                    sender.Send(new ListShortLinkAuditActionsQuery(), cancellationToken))
            .WithTags("Audit Logs")
            .WithName("ListAuditLogActions");

        return endpoints;
    }
}
