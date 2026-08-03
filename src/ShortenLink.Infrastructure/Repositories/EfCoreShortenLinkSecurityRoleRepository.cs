using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkSecurityRoleRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortenLinkCustomRolePersistenceEntity>(dbContext),
        IShortenLinkSecurityRoleRepository
{
    public async Task<IReadOnlyList<ShortenLinkCustomRole>> ListCustomRolesAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await ReadOnlyEntities
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

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(role => role.RoleId == id, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task<IReadOnlyDictionary<string, ShortenLinkCustomRole>> FindCustomRolesAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var roleIds = ids
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (roleIds.Length == 0)
        {
            return new Dictionary<string, ShortenLinkCustomRole>(StringComparer.Ordinal);
        }

        var records = await ReadOnlyEntities
            .Where(role => roleIds.Contains(role.RoleId))
            .ToListAsync(cancellationToken);
        return records
            .Select(role => role.ToDomain())
            .ToDictionary(role => role.RoleKey, StringComparer.Ordinal);
    }

    public async Task AddOrUpdateCustomRoleAsync(
        ShortenLinkCustomRole role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        var record = await Entities
            .FirstOrDefaultAsync(candidate => candidate.RoleId == role.RoleKey, cancellationToken)
            ;

        if (record is null)
        {
            Entities.Add(ShortenLinkCustomRolePersistenceEntity.FromDomain(role));
        }
        else
        {
            record.UpdateFromDomain(role);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShortenLinkRolePermissionOverride>> ListPermissionOverridesAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        return await DbContext.SecurityRolePermissionOverrides
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .OrderBy(item => item.Permission)
            .Select(item => new ShortenLinkRolePermissionOverride(item.Permission, item.IsAllowed))
            .ToListAsync(cancellationToken)
            ;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ShortenLinkRolePermissionOverride>>> ListPermissionOverridesAsync(
        IReadOnlyCollection<string> roleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        var ids = roleIds
            .Where(static roleId => !string.IsNullOrWhiteSpace(roleId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, IReadOnlyList<ShortenLinkRolePermissionOverride>>(StringComparer.Ordinal);
        }

        var records = await DbContext.SecurityRolePermissionOverrides
            .AsNoTracking()
            .Where(item => ids.Contains(item.RoleId))
            .OrderBy(item => item.RoleId)
            .ThenBy(item => item.Permission)
            .Select(item => new
            {
                item.RoleId,
                Override = new ShortenLinkRolePermissionOverride(item.Permission, item.IsAllowed)
            })
            .ToListAsync(cancellationToken);

        return records
            .GroupBy(item => item.RoleId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ShortenLinkRolePermissionOverride>)group
                    .Select(item => item.Override)
                    .ToList(),
                StringComparer.Ordinal);
    }

    public async Task ReplacePermissionOverridesAsync(
        string roleId,
        IReadOnlyList<ShortenLinkRolePermissionOverride> overrides,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentNullException.ThrowIfNull(overrides);

        var current = await DbContext.SecurityRolePermissionOverrides
            .Where(item => item.RoleId == roleId)
            .ToListAsync(cancellationToken)
            ;
        DbContext.SecurityRolePermissionOverrides.RemoveRange(current);
        DbContext.SecurityRolePermissionOverrides.AddRange(overrides.Select(item => new ShortenLinkRolePermissionOverridePersistenceEntity
        {
            RoleId = roleId,
            Permission = item.Permission,
            IsAllowed = item.IsAllowed
        }));
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteCustomRoleAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await Entities
            .FirstOrDefaultAsync(role => role.RoleId == id, cancellationToken)
            ;
        if (record is null)
        {
            return false;
        }

        Entities.Remove(record);
        var overrides = await DbContext.SecurityRolePermissionOverrides
            .Where(item => item.RoleId == id)
            .ToListAsync(cancellationToken)
            ;
        DbContext.SecurityRolePermissionOverrides.RemoveRange(overrides);
        await SaveChangesAsync(cancellationToken);
        return true;
    }
}
