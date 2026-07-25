using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkSecurityUserRepository : IShortenLinkSecurityUserRepository
{
    public const string BootstrapAdminUserId = "bootstrap-admin";
    public const string BootstrapAdminUsername = "admin@shortenlink.local";

    private readonly ShortLinkDbContext dbContext;

    public EfCoreShortenLinkSecurityUserRepository(ShortLinkDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ShortenLinkSecurityUser>> ListAsync(
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SecurityUsers.AsNoTracking();
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

        var record = await dbContext.SecurityUsers
            .AsNoTracking()
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

        var record = await dbContext.SecurityUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username == normalizedUsername, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task AddOrUpdateAsync(
        ShortenLinkSecurityUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var record = await dbContext.SecurityUsers
            .FirstOrDefaultAsync(candidate => candidate.UserId == user.UserKey, cancellationToken)
            ;

        if (record is null)
        {
            dbContext.SecurityUsers.Add(ShortenLinkSecurityUserPersistenceEntity.FromDomain(user));
        }
        else
        {
            record.UpdateFromDomain(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShortenLinkSecurityUser> EnsureBootstrapAdminAsync(
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var existing = await dbContext.SecurityUsers
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

            dbContext.SecurityUsers.Add(ShortenLinkSecurityUserPersistenceEntity.FromDomain(user));
            await dbContext.SaveChangesAsync(cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToDomain();
    }

    public async Task<bool> DisableAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await dbContext.SecurityUsers
            .FirstOrDefaultAsync(user => user.UserId == id, cancellationToken)
            ;
        if (record is null || record.IsBootstrap)
        {
            return false;
        }

        record.IsEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
