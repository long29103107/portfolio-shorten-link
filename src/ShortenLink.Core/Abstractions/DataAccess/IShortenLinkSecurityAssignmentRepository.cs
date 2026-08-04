using ShortenLink.Core.Security;

namespace ShortenLink.Core.Abstractions;

public interface IShortenLinkSecurityAssignmentRepository
{
    Task<IReadOnlyList<ShortenLinkSecurityAssignment>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ShortenLinkSecurityAssignment?> FindByCredentialKeyHashAsync(
        string credentialKeyHash,
        CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(
        ShortenLinkSecurityAssignment assignment,
        CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(
        string credentialKeyHash,
        CancellationToken cancellationToken = default);
}
