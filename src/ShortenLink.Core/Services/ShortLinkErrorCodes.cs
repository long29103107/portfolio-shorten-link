namespace ShortenLink.Core.Services;

public static class ShortLinkErrorCodes
{
    public const string Expired = "expired";
    public const string Inactive = "inactive";
    public const string InvalidCode = "invalid_code";
    public const string InvalidExpiration = "invalid_expiration";
    public const string InvalidUrl = "invalid_url";
    public const string InvalidIdempotencyKey = "invalid_idempotency_key";
    public const string InvalidTenantId = "invalid_tenant_id";
    public const string ExpirationHandoffUnavailable = "expiration_handoff_unavailable";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string IdempotencyNotSupported = "idempotency_not_supported";
    public const string TenantNotSupported = "tenant_not_supported";
    public const string NotFound = "not_found";
    public const string UnableToGenerateCode = "unable_to_generate_code";
}
