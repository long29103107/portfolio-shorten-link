namespace ShortenLink.Core;

public static class ShortLinkTenantId
{
    public const int MaxLength = 128;

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= MaxLength ? normalized : null;
    }

    public static bool IsValid(string? value) =>
        string.IsNullOrWhiteSpace(value) || Normalize(value) is not null;
}
