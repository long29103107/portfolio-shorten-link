using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Hosting;

internal sealed class DatabaseInit : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly bool initializeSecurity;

    public DatabaseInit(IServiceScopeFactory scopeFactory, bool initializeSecurity = true)
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
        await ShortLinkDatabaseSchema.EnsureExpirationCheckpointsTableAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureIdempotencySchemaAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureScheduledActivationSchemaAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureClickLimitSchemaAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsurePasswordProtectionSchemaAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureAdvancedAnalyticsSchemaAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureOrganizationSchemaAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureBulkJobsTableAsync(dbContext, cancellationToken);
        await ShortLinkDatabaseSchema.EnsureUtcTimestampSchemaAsync(dbContext, cancellationToken);

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
