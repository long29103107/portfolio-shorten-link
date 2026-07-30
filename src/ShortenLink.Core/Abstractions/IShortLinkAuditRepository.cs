using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkAuditRepository
{
    Task AddAsync(
        ShortLinkAuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    Task<ShortLinkAuditPage> ListAsync(
        ShortLinkAuditQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListActionsAsync(
        ShortLinkAuditAccessScope accessScope,
        CancellationToken cancellationToken = default);
}
