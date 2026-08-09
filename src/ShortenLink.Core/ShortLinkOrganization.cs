namespace ShortenLink.Core;

public static class ShortLinkOrganization
{
    public const int MaxFolderLength = 128;
    public const int MaxTagLength = 64;
    public const int MaxTags = 20;
    public const int MaxSerializedTagsLength = 2048;

    public static bool TryNormalize(
        string? folder,
        IEnumerable<string>? tags,
        out string? normalizedFolder,
        out IReadOnlyList<string> normalizedTags,
        out string? errorCode,
        out string? errorMessage)
    {
        normalizedFolder = NormalizeFolder(folder);
        normalizedTags = NormalizeTags(tags, out errorCode, out errorMessage);
        if (errorCode is not null)
        {
            return false;
        }

        if (normalizedFolder is not null && normalizedFolder.Length > MaxFolderLength)
        {
            errorCode = Services.ShortLinkErrorCodes.InvalidFolder;
            errorMessage = $"Folder must be at most {MaxFolderLength} characters.";
            return false;
        }

        errorCode = null;
        errorMessage = null;
        return true;
    }

    public static string? NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var normalized = folder.Trim().ToLowerInvariant();
        return normalized.Length > 0 ? normalized : null;
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags) =>
        NormalizeTags(tags, out _, out _);

    public static string SerializeTags(IEnumerable<string>? tags) =>
        SerializeTags(NormalizeTags(tags));

    public static string SerializeTags(IReadOnlyList<string> tags) =>
        tags.Count == 0 ? string.Empty : $"|{string.Join('|', tags)}|";

    public static IReadOnlyList<string> ParseTags(string? serializedTags) =>
        string.IsNullOrWhiteSpace(serializedTags)
            ? []
            : serializedTags
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(static tag => tag.ToLowerInvariant())
                .ToList();

    public static string SerializeSingleTag(string tag) =>
        $"|{NormalizeTags([tag]).Single()}|";

    private static IReadOnlyList<string> NormalizeTags(
        IEnumerable<string>? tags,
        out string? errorCode,
        out string? errorMessage)
    {
        errorCode = null;
        errorMessage = null;
        var normalized = new List<string>();
        foreach (var tag in tags ?? [])
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var value = tag.Trim().ToLowerInvariant();
            if (value.Contains('|', StringComparison.Ordinal))
            {
                errorCode = Services.ShortLinkErrorCodes.InvalidTags;
                errorMessage = "Tags cannot contain the pipe character.";
                return [];
            }

            if (value.Length > MaxTagLength)
            {
                errorCode = Services.ShortLinkErrorCodes.InvalidTags;
                errorMessage = $"Each tag must be at most {MaxTagLength} characters.";
                return [];
            }

            if (!normalized.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(value);
            }
        }

        if (normalized.Count > MaxTags)
        {
            errorCode = Services.ShortLinkErrorCodes.InvalidTags;
            errorMessage = $"A short link can have at most {MaxTags} tags.";
            return [];
        }

        if (SerializeTags(normalized).Length > MaxSerializedTagsLength)
        {
            errorCode = Services.ShortLinkErrorCodes.InvalidTags;
            errorMessage = $"The serialized tag metadata must be at most {MaxSerializedTagsLength} characters.";
            return [];
        }

        return normalized;
    }
}
