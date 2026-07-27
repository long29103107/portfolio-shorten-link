using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortenLinkUserApiKeyPersistenceEntity : BaseEntity<Guid>
{
    public string ApiKeyId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public static ShortenLinkUserApiKeyPersistenceEntity FromDomain(ShortenLinkUserApiKey apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        return new ShortenLinkUserApiKeyPersistenceEntity
        {
            Id = apiKey.Id,
            ApiKeyId = apiKey.ApiKeyKey,
            UserId = apiKey.UserId,
            DisplayName = apiKey.DisplayName,
            KeyHash = apiKey.KeyHash,
            IsEnabled = apiKey.IsEnabled,
            CreatedAt = apiKey.CreatedAt
        };
    }

    public ShortenLinkUserApiKey ToDomain() =>
        new(
            ApiKeyId,
            UserId,
            DisplayName,
            KeyHash,
            IsEnabled,
            CreatedAt,
            Id);

    public void UpdateFromDomain(ShortenLinkUserApiKey apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        UserId = apiKey.UserId;
        DisplayName = apiKey.DisplayName;
        KeyHash = apiKey.KeyHash;
        IsEnabled = apiKey.IsEnabled;
        CreatedAt = apiKey.CreatedAt;
    }
}
