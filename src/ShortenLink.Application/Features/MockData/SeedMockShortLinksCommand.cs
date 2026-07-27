using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.MockData;

public sealed record SeedMockShortLinksCommand(int? Count) : IRequest<SeedMockShortLinksResult>;

public sealed record SeedMockShortLinksResult(
    int RequestedCount,
    int CreatedCount,
    int FailedCount,
    IReadOnlyList<string> Codes);

internal sealed class SeedMockShortLinksCommandHandler(
    IShortLinkService shortLinkService,
    ICurrentRequestContext requestContext,
    TimeProvider timeProvider) : IRequestHandler<SeedMockShortLinksCommand, SeedMockShortLinksResult>
{
    public async Task<SeedMockShortLinksResult> Handle(
        SeedMockShortLinksCommand request,
        CancellationToken cancellationToken)
    {
        var currentUser = await requestContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is not null)
        {
            await requestContext.EnsureAuthorizedAsync(
                ShortenLinkPermissionCatalog.ShortLinksCreate,
                cancellationToken);
        }

        var requestedCount = Math.Clamp(request.Count ?? 200, 1, 500);
        var createdCodes = new List<string>(requestedCount);
        var failedCount = 0;
        var expiresAtUtc = timeProvider.GetUtcNow().AddDays(30);

        for (var index = 1; index <= requestedCount; index++)
        {
            var result = await shortLinkService.CreateAsync(
                new CreateShortLinkRequest(
                    CreateMockUrl(index),
                    expiresAtUtc,
                    currentUser?.UserId,
                    currentUser?.DisplayName,
                    currentUser?.Username),
                cancellationToken);

            if (result.Succeeded && result.ShortLink is not null)
            {
                createdCodes.Add(result.ShortLink.Code);
            }
            else
            {
                failedCount++;
            }
        }

        return new SeedMockShortLinksResult(
            requestedCount,
            createdCodes.Count,
            failedCount,
            createdCodes);
    }

    internal static string CreateMockUrl(int index)
    {
        var normalizedIndex = Math.Max(index, 1);
        string[] domains =
        [
            "https://example.com",
            "https://github.com",
            "https://learn.microsoft.com",
            "https://www.episoden.com",
            "https://docs.example.dev"
        ];
        var domain = domains[(normalizedIndex - 1) % domains.Length];
        return $"{domain}/mock/short-link/{normalizedIndex:000}?source=seed";
    }
}
