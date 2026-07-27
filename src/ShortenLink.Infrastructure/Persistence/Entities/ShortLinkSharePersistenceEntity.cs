using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkSharePersistenceEntity : BaseEntity<Guid>
{
    public string ShortCode { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public ShortLinkShareAccess Access { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public static ShortLinkSharePersistenceEntity FromDomain(ShortLinkShare share)
    {
        ArgumentNullException.ThrowIfNull(share);

        return new ShortLinkSharePersistenceEntity
        {
            ShortCode = share.ShortCode,
            UserId = share.UserId,
            Access = share.Access,
            CreatedByUserId = share.CreatedByUserId,
            CreatedAt = share.CreatedAt
        };
    }

    public void UpdateFromDomain(ShortLinkShare share)
    {
        ArgumentNullException.ThrowIfNull(share);

        Access = share.Access;
        CreatedByUserId = share.CreatedByUserId;
        CreatedAt = share.CreatedAt;
    }

    public ShortLinkShare ToDomain() =>
        new(ShortCode, UserId, Access, CreatedByUserId, CreatedAt);
}
