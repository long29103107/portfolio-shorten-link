using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Hosting;

internal sealed class ShortLinkDatabaseInitializationService : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly bool initializeSecurity;

    public ShortLinkDatabaseInitializationService(IServiceScopeFactory scopeFactory, bool initializeSecurity = true)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.initializeSecurity = initializeSecurity;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await ShortLinkDatabaseSchema.EnsureAuditEventsTableAsync(dbContext, cancellationToken);

        if (initializeSecurity)
        {
            var userRepository = scope.ServiceProvider.GetRequiredService<IShortenLinkSecurityUserRepository>();
            await userRepository.EnsureBootstrapAdminAsync(
                ShortenLinkSecurityCredentialHasher.HashPassword("admin"),
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
