using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;
using Xunit;

namespace ShortenLink.Core.Tests.Contracts;

/// <summary>
/// Provider contract suite. A persistence adapter can inherit this fixture and
/// implement <see cref="CreateRepositoryAsync"/> to run the same lifecycle
/// assertions against its own store.
/// </summary>
public abstract class ShortLinkRepositoryContractTests
{
    protected abstract Task<IShortLinkRepository> CreateRepositoryAsync();

    protected virtual Task<IUnitOfWork?> CreateUnitOfWorkAsync() =>
        Task.FromResult<IUnitOfWork?>(null);

    [Fact]
    public async Task AddFindAndExistsMustRoundTripByCode()
    {
        var repository = await CreateRepositoryAsync();
        var link = NewLink("contract-roundtrip");

        await repository.AddAsync(link);

        var found = await repository.FindByCodeAsync(link.Code);
        Assert.NotNull(found);
        Assert.Equal(link.OriginalUrl, found!.OriginalUrl);
        Assert.True(await repository.ExistsByCodeAsync(link.Code));
    }

    [Fact]
    public async Task UpdateMustPreserveCodeAndReplaceMutableState()
    {
        var repository = await CreateRepositoryAsync();
        var link = NewLink("contract-update");
        await repository.AddAsync(link);

        link.Deactivate();
        await repository.UpdateAsync(link);

        var found = await repository.FindByCodeAsync(link.Code);
        Assert.NotNull(found);
        Assert.False(found!.IsActive);
    }

    [Fact]
    public async Task DeleteMustRemoveTheLinkAndExistenceIndex()
    {
        var repository = await CreateRepositoryAsync();
        var link = NewLink("contract-delete");
        await repository.AddAsync(link);

        await repository.DeleteAsync(link.Code);

        Assert.Null(await repository.FindByCodeAsync(link.Code));
        Assert.False(await repository.ExistsByCodeAsync(link.Code));
    }

    [Fact]
    public void ExpiredLinksMustRemainExpiredAccordingToTheDomainContract()
    {
        var link = new ShortLink(
            "contract-expired",
            new Uri("https://example.com/contract-expired"),
            DateTimeOffset.UtcNow.AddMinutes(-2),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(link.IsExpired(DateTimeOffset.UtcNow));
        Assert.False(link.CanResolve(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task DuplicateCodesMustBeRejectedByTheProvider()
    {
        var repository = await CreateRepositoryAsync();
        await repository.AddAsync(NewLink("contract-duplicate"));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            repository.AddAsync(NewLink("contract-duplicate")));
    }

    [Fact]
    public async Task UnitOfWorkMustReturnTheOperationResult()
    {
        var unitOfWork = await CreateUnitOfWorkAsync();
        if (unitOfWork is null)
        {
            return;
        }

        var result = await unitOfWork.ExecuteAsync(_ => Task.FromResult(42));
        Assert.Equal(42, result);
    }

    private static ShortLink NewLink(string code) => new(
        code,
        new Uri("https://example.com/" + code),
        DateTimeOffset.UtcNow,
        expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));
}
