using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkSecurityUserRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortenLinkSecurityUserPersistenceEntity>(dbContext),
        IShortenLinkSecurityUserRepository
{
    public const string BootstrapAdminUserId = "bootstrap-admin";
    public const string BootstrapAdminUsername = "admin@shortenlink.local";

    public async Task<IReadOnlyList<ShortenLinkSecurityUser>> ListAsync(
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var query = ReadOnlyEntities;
        if (!includeHidden)
        {
            query = query.Where(user => !user.IsHidden);
        }

        var records = await query
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.UserId, StringComparer.Ordinal)
            .Select(user => user.ToDomain())
            .ToList();
    }

    public async Task<ShortenLinkSecurityUser?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(user => user.UserId == id, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task<ShortenLinkSecurityUser?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var normalizedUsername = username.Trim();

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(user => user.Username == normalizedUsername, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task AddOrUpdateAsync(
        ShortenLinkSecurityUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var record = await Entities
            .FirstOrDefaultAsync(candidate => candidate.UserId == user.UserKey, cancellationToken)
            ;

        if (record is null)
        {
            Entities.Add(ShortenLinkSecurityUserPersistenceEntity.FromDomain(user));
        }
        else
        {
            record.UpdateFromDomain(user);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task<ShortenLinkSecurityUser> EnsureBootstrapAdminAsync(
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var existing = await Entities
            .FirstOrDefaultAsync(
                user => user.UserId == BootstrapAdminUserId || user.IsBootstrap,
                cancellationToken)
            ;

        if (existing is null)
        {
            var user = new ShortenLinkSecurityUser(
                BootstrapAdminUserId,
                BootstrapAdminUsername,
                "Bootstrap Admin",
                passwordHash,
                new[] { ShortenLinkSystemRoles.Admin },
                isEnabled: true,
                isHidden: true,
                isBootstrap: true,
                createdAt);

            Entities.Add(ShortenLinkSecurityUserPersistenceEntity.FromDomain(user));
            await SaveChangesAsync(cancellationToken);
            return user;
        }

        existing.PasswordHash = passwordHash;
        existing.Username = BootstrapAdminUsername;
        existing.DisplayName = string.IsNullOrWhiteSpace(existing.DisplayName)
            ? "Bootstrap Admin"
            : existing.DisplayName;
        existing.RoleIdsJson = "[\"Admin\"]";
        existing.IsEnabled = true;
        existing.IsHidden = true;
        existing.IsBootstrap = true;

        await SaveChangesAsync(cancellationToken);
        return existing.ToDomain();
    }

    public async Task<bool> DisableAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await Entities
            .FirstOrDefaultAsync(user => user.UserId == id, cancellationToken)
            ;
        if (record is null || record.IsBootstrap)
        {
            return false;
        }

        record.IsEnabled = false;
        await SaveChangesAsync(cancellationToken);
        return true;
    }
}
