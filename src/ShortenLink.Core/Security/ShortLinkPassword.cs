namespace ShortenLink.Core.Security;

public static class ShortLinkPassword
{
    public const int MaxLength = 256;

    public static bool IsValid(string? password) =>
        password is null
        || !string.IsNullOrWhiteSpace(password) && password.Length <= MaxLength;
}
