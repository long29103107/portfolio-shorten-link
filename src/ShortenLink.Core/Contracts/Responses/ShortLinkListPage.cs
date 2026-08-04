using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Contracts.Responses;

public sealed record ShortLinkListPage(
    IReadOnlyList<ShortLink> Items,
    int TotalCount);
