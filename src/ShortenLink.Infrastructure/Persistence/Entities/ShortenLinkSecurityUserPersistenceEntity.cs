using System.Text.Json;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortenLinkSecurityUserPersistenceEntity : BaseEntity<Guid>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string RoleIdsJson { get; set; } = "[]";

    public bool IsEnabled { get; set; }

    public bool IsHidden { get; set; }

    public bool IsBootstrap { get; set; }

    public static ShortenLinkSecurityUserPersistenceEntity FromDomain(ShortenLinkSecurityUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new ShortenLinkSecurityUserPersistenceEntity
        {
            Id = user.Id,
            UserId = user.UserKey,
            Username = user.Username,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            RoleIdsJson = JsonSerializer.Serialize(user.RoleIds, SerializerOptions),
            IsEnabled = user.IsEnabled,
            IsHidden = user.IsHidden,
            IsBootstrap = user.IsBootstrap,
            CreatedAt = user.CreatedAt
        };
    }

    public ShortenLinkSecurityUser ToDomain() =>
        new(
            UserId,
            Username,
            DisplayName,
            PasswordHash,
            DeserializeList(RoleIdsJson),
            IsEnabled,
            IsHidden,
            IsBootstrap,
            CreatedAt,
            Id);

    public void UpdateFromDomain(ShortenLinkSecurityUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        Username = user.Username;
        DisplayName = user.DisplayName;
        PasswordHash = user.PasswordHash;
        RoleIdsJson = JsonSerializer.Serialize(user.RoleIds, SerializerOptions);
        IsEnabled = user.IsEnabled;
        IsHidden = user.IsHidden;
        IsBootstrap = user.IsBootstrap;
        CreatedAt = user.CreatedAt;
    }

    private static IReadOnlyList<string> DeserializeList(string value) =>
        JsonSerializer.Deserialize<List<string>>(value, SerializerOptions) ?? new List<string>();
}
