using System.Text.Json;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortenLinkCustomRolePersistenceEntity : BaseEntity<Guid>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string RoleId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PermissionsJson { get; set; } = "[]";

    public bool IsEnabled { get; set; }

    public static ShortenLinkCustomRolePersistenceEntity FromDomain(ShortenLinkCustomRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new ShortenLinkCustomRolePersistenceEntity
        {
            Id = role.Id,
            RoleId = role.RoleKey,
            Name = role.Name,
            PermissionsJson = JsonSerializer.Serialize(role.Permissions, SerializerOptions),
            IsEnabled = role.IsEnabled,
            CreatedAt = role.CreatedAt
        };
    }

    public ShortenLinkCustomRole ToDomain() =>
        new(
            RoleId,
            Name,
            DeserializeList(PermissionsJson),
            IsEnabled,
            CreatedAt,
            Id);

    public void UpdateFromDomain(ShortenLinkCustomRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        Name = role.Name;
        PermissionsJson = JsonSerializer.Serialize(role.Permissions, SerializerOptions);
        IsEnabled = role.IsEnabled;
        CreatedAt = role.CreatedAt;
    }

    private static IReadOnlyList<string> DeserializeList(string value) =>
        JsonSerializer.Deserialize<List<string>>(value, SerializerOptions) ?? new List<string>();
}
