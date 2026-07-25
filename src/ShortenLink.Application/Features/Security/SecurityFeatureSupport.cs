using ShortenLink.Application.Abstractions;

namespace ShortenLink.Application.Features.Security;

internal static class SecurityFeatureSupport
{
    internal const string AdminOnly = "$admin";

    internal static Task EnsureAdminAsync(
        this ICurrentRequestContext requestContext,
        CancellationToken cancellationToken) =>
        requestContext.EnsureAuthorizedAsync(AdminOnly, cancellationToken);

    internal static IReadOnlyList<string> NormalizeDistinct(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    internal static RequestValidationException Validation(
        string code,
        string message,
        string field) =>
        new(
            code,
            message,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [field] = [message]
            });
}
