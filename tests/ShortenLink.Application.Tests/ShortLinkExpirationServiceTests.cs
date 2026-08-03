using ShortenLink.Application.Services;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Contracts.Results;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Events;
using ShortenLink.Core.Services;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class ShortLinkExpirationServiceTests
{
    [Fact]
    public async Task EvaluateBatchAsync_IsReadOnlyTenantScopedAndPublishesExpiredHook()
    {
        var evaluatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var repository = new ExpirationRepository(
            new ShortLink(
                "expired1",
                new Uri("https://example.com/expired"),
                evaluatedAt.AddDays(-3),
                evaluatedAt.AddDays(-1),
                tenantId: "tenant-a"),
            new ShortLink(
                "other01",
                new Uri("https://example.com/other"),
                evaluatedAt.AddDays(-3),
                evaluatedAt.AddDays(-1),
                tenantId: "tenant-b"));
        var sink = new CapturingExpirationSink();
        var service = new ShortLinkExpirationService(
            repository,
            new ShortLinkExpirationEvaluator(),
            sink);

        var result = await service.EvaluateBatchAsync(
            new ShortLinkExpirationBatchRequest(evaluatedAt, TenantId: "tenant-a"));

        Assert.Equal("expired1", Assert.Single(result.Items).Code);
        Assert.Equal(ShortLinkExpirationOutcome.Expired, result.Items[0].Outcome);
        Assert.Equal("expired1", Assert.Single(sink.Events).Code);
        Assert.Empty(repository.MutatedCodes);
    }

    private sealed class CapturingExpirationSink : IShortLinkExpirationEventSink
    {
        public List<ShortLinkExpirationEvent> Events { get; } = [];

        public bool TryPublish(
            ShortLinkExpirationEvent @event,
            CancellationToken cancellationToken = default)
        {
            Events.Add(@event);
            return true;
        }
    }

    private sealed class ExpirationRepository(params ShortLink[] links)
        : IShortLinkRepository, IShortLinkExpirationRepository
    {
        private readonly List<ShortLink> records = links.ToList();

        public List<string> MutatedCodes { get; } = [];

        public Task<IReadOnlyList<ShortLink>> ListRecentAsync(
            int limit,
            DateTimeOffset? beforeCreatedAt = null,
            string? beforeCode = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShortLink>>(records);

        public Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
            int skip,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShortLink>>(records.Skip(skip).Take(limit).ToList());

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(records.Count);

        public Task<ShortLinkListPage> ListPageAsync(
            int skip,
            int limit,
            ShortLinkListQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShortLinkListPage(records, records.Count));

        public Task<ShortLink?> FindByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(link => link.Code == code));

        public Task<bool> ExistsByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(records.Any(link => link.Code == code));

        public Task AddAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
        {
            MutatedCodes.Add(shortLink.Code);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
        {
            MutatedCodes.Add(shortLink.Code);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
        {
            MutatedCodes.Add(code);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ShortLink>> ListExpirationCandidatesAsync(
            string? tenantId,
            DateTimeOffset? beforeExpiresAt,
            string? beforeCode,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShortLink>>(
                records
                    .Where(link => string.Equals(link.TenantId, tenantId, StringComparison.Ordinal))
                    .OrderBy(link => link.ExpiresAt)
                    .ThenBy(link => link.Code, StringComparer.Ordinal)
                    .ToList());
    }
}
