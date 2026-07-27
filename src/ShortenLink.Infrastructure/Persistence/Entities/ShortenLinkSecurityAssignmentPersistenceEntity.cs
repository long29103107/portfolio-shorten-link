using System.Text.Json;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortenLinkSecurityAssignmentPersistenceEntity : BaseEntity<Guid>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string CredentialKeyHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RolesJson { get; set; } = "[]";

    public string PermissionsJson { get; set; } = "[]";

    public bool IsEnabled { get; set; }

    public static ShortenLinkSecurityAssignmentPersistenceEntity FromDomain(ShortenLinkSecurityAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return new ShortenLinkSecurityAssignmentPersistenceEntity
        {
            Id = assignment.Id,
            CredentialKeyHash = assignment.CredentialKeyHash,
            Name = assignment.Name,
            RolesJson = JsonSerializer.Serialize(assignment.Roles, SerializerOptions),
            PermissionsJson = JsonSerializer.Serialize(assignment.Permissions, SerializerOptions),
            IsEnabled = assignment.IsEnabled,
            CreatedAt = assignment.CreatedAt
        };
    }

    public ShortenLinkSecurityAssignment ToDomain() =>
        new(
            CredentialKeyHash,
            Name,
            DeserializeList(RolesJson),
            DeserializeList(PermissionsJson),
            IsEnabled,
            CreatedAt,
            Id);

    public void UpdateFromDomain(ShortenLinkSecurityAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        Name = assignment.Name;
        RolesJson = JsonSerializer.Serialize(assignment.Roles, SerializerOptions);
        PermissionsJson = JsonSerializer.Serialize(assignment.Permissions, SerializerOptions);
        IsEnabled = assignment.IsEnabled;
        CreatedAt = assignment.CreatedAt;
    }

    private static IReadOnlyList<string> DeserializeList(string value) =>
        JsonSerializer.Deserialize<List<string>>(value, SerializerOptions) ?? new List<string>();
}
