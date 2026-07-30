using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkSecurityAssignmentRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortenLinkSecurityAssignmentPersistenceEntity>(dbContext),
        IShortenLinkSecurityAssignmentRepository
{
    public async Task<IReadOnlyList<ShortenLinkSecurityAssignment>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await ReadOnlyEntities
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderBy(assignment => assignment.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(assignment => assignment.CredentialKeyHash, StringComparer.Ordinal)
            .Select(record => record.ToDomain())
            .ToList();
    }

    public async Task<ShortenLinkSecurityAssignment?> FindByCredentialKeyHashAsync(
        string credentialKeyHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKeyHash);

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(
                assignment => assignment.CredentialKeyHash == credentialKeyHash,
                cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task AddOrUpdateAsync(
        ShortenLinkSecurityAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var record = await Entities
            .FirstOrDefaultAsync(
                candidate => candidate.CredentialKeyHash == assignment.CredentialKeyHash,
                cancellationToken)
            ;

        if (record is null)
        {
            Entities.Add(ShortenLinkSecurityAssignmentPersistenceEntity.FromDomain(assignment));
        }
        else
        {
            record.UpdateFromDomain(assignment);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DisableAsync(
        string credentialKeyHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialKeyHash);

        var record = await Entities
            .FirstOrDefaultAsync(
                assignment => assignment.CredentialKeyHash == credentialKeyHash,
                cancellationToken)
            ;
        if (record is null)
        {
            return false;
        }

        record.IsEnabled = false;
        await SaveChangesAsync(cancellationToken);
        return true;
    }
}
