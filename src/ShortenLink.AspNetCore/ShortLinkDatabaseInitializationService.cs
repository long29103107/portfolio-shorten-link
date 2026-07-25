using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.AspNetCore;

internal sealed class ShortLinkDatabaseInitializationService : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;

    public ShortLinkDatabaseInitializationService(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var userRepository = scope.ServiceProvider.GetRequiredService<IShortenLinkSecurityUserRepository>();
        await userRepository
            .EnsureBootstrapAdminAsync(
                ShortenLinkSecurityCredentialHasher.HashPassword("admin"),
                DateTimeOffset.UtcNow,
                cancellationToken)
            ;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
