using System.Linq.Expressions;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.ReadModels;

internal sealed record ShortLinkPersistenceReadModel(
    Guid Id,
    string Code,
    string OriginalUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ActiveFrom,
    int? MaxClicks,
    int ClickCount,
    bool IsActive,
    string? CreatedByUserId,
    string? CreatedByDisplayName,
    string? CreatedByUsername,
    string TenantId,
    ShortLinkSharingMode SharingMode)
{
    public static Expression<Func<ShortLinkPersistenceEntity, ShortLinkPersistenceReadModel>>
        Projection { get; } = entity => new(
            entity.Id,
            entity.Code,
            entity.OriginalUrl,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.ActiveFrom,
            entity.MaxClicks,
            entity.ClickCount,
            entity.IsActive,
            entity.CreatedByUserId,
            entity.CreatedByDisplayName,
            entity.CreatedByUsername,
            entity.TenantId,
            entity.SharingMode);

    public ShortLink ToDomain() =>
        new(
            Code,
            new Uri(OriginalUrl),
            CreatedAt,
            ExpiresAt,
            IsActive,
            CreatedByUserId,
            CreatedByDisplayName,
            CreatedByUsername,
            Id,
            tenantId: TenantId,
            sharingMode: SharingMode,
            activeFrom: ActiveFrom,
            maxClicks: MaxClicks,
            clickCount: ClickCount);
}

internal sealed record ShortLinkClickPersistenceReadModel(
    Guid Id,
    string ShortCode,
    string TenantId,
    DateTimeOffset ClickedAtUtc,
    string? RemoteIpAddress,
    string? UserAgent,
    string? Referrer)
{
    public static Expression<Func<ShortLinkClickPersistenceEntity, ShortLinkClickPersistenceReadModel>>
        Projection { get; } = entity => new(
            entity.Id,
            entity.ShortCode,
            entity.TenantId,
            entity.ClickedAtUtc,
            entity.RemoteIpAddress,
            entity.UserAgent,
            entity.Referrer);

    public ShortLinkClick ToDomain() =>
        new(ShortCode, ClickedAtUtc, RemoteIpAddress, UserAgent, Referrer, Id, TenantId);
}

internal sealed record AuditEventPersistenceReadModel(
    Guid Id,
    string ActorId,
    string Action,
    string TargetType,
    string TargetId,
    string? OwnerUserId,
    string Outcome,
    DateTimeOffset OccurredAt,
    string? SubjectUserId,
    string? Detail)
{
    public static Expression<Func<AuditEventPersistenceEntity, AuditEventPersistenceReadModel>>
        Projection { get; } = entity => new(
            entity.Id,
            entity.ActorId,
            entity.Action,
            entity.TargetType,
            entity.TargetId,
            entity.OwnerUserId,
            entity.Outcome,
            entity.OccurredAt,
            entity.SubjectUserId,
            entity.Detail);

    public AuditEvent ToDomain() =>
        new(
            ActorId,
            Action,
            TargetId,
            OwnerUserId,
            OccurredAt,
            Outcome,
            SubjectUserId,
            Detail,
            Id,
            TargetType);
}
