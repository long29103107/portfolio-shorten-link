using ShortenLink.Core.Domain;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortenLinkRolePermissionOverridePersistenceEntity : BaseEntity<Guid>
{
    public string RoleId { get; set; } = string.Empty;

    public string Permission { get; set; } = string.Empty;

    public bool IsAllowed { get; set; }
}
