using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkSecurityRoleRepository : IShortenLinkSecurityRoleRepository
{
    private readonly ShortLinkDbContext dbContext;

    public EfCoreShortenLinkSecurityRoleRepository(ShortLinkDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ShortenLinkCustomRole>> ListCustomRolesAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await dbContext.SecurityCustomRoles
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleId, StringComparer.Ordinal)
            .Select(role => role.ToDomain())
            .ToList();
    }

    public async Task<ShortenLinkCustomRole?> FindCustomRoleAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await dbContext.SecurityCustomRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.RoleId == id, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task AddOrUpdateCustomRoleAsync(
        ShortenLinkCustomRole role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        var record = await dbContext.SecurityCustomRoles
            .FirstOrDefaultAsync(candidate => candidate.RoleId == role.RoleKey, cancellationToken)
            ;

        if (record is null)
        {
            dbContext.SecurityCustomRoles.Add(ShortenLinkCustomRolePersistenceEntity.FromDomain(role));
        }
        else
        {
            record.UpdateFromDomain(role);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShortenLinkRolePermissionOverride>> ListPermissionOverridesAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        return await dbContext.SecurityRolePermissionOverrides
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .OrderBy(item => item.Permission)
            .Select(item => new ShortenLinkRolePermissionOverride(item.Permission, item.IsAllowed))
            .ToListAsync(cancellationToken)
            ;
    }

    public async Task ReplacePermissionOverridesAsync(
        string roleId,
        IReadOnlyList<ShortenLinkRolePermissionOverride> overrides,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentNullException.ThrowIfNull(overrides);

        var current = await dbContext.SecurityRolePermissionOverrides
            .Where(item => item.RoleId == roleId)
            .ToListAsync(cancellationToken)
            ;
        dbContext.SecurityRolePermissionOverrides.RemoveRange(current);
        dbContext.SecurityRolePermissionOverrides.AddRange(overrides.Select(item => new ShortenLinkRolePermissionOverridePersistenceEntity
        {
            RoleId = roleId,
            Permission = item.Permission,
            IsAllowed = item.IsAllowed
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteCustomRoleAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await dbContext.SecurityCustomRoles
            .FirstOrDefaultAsync(role => role.RoleId == id, cancellationToken)
            ;
        if (record is null)
        {
            return false;
        }

        dbContext.SecurityCustomRoles.Remove(record);
        var overrides = await dbContext.SecurityRolePermissionOverrides
            .Where(item => item.RoleId == id)
            .ToListAsync(cancellationToken)
            ;
        dbContext.SecurityRolePermissionOverrides.RemoveRange(overrides);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
