namespace ShortenLink.Core.Services;

public static class ShortLinkImportErrorCodes
{
    public const string InvalidUrl = "invalid_url";
    public const string InvalidExpiration = "invalid_expiration";
    public const string InvalidIdempotencyKey = "invalid_idempotency_key";
    public const string DuplicateIdempotencyKey = "duplicate_idempotency_key";
    public const string ImportLimitExceeded = "import_limit_exceeded";
    public const string PersistenceFailure = "persistence_failure";
}

public static class ShortLinkImportLimits
{
    public const int MaxDryRunItems = 1000;
}
