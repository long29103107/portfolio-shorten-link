# ShortenLink.Auditing

`ShortenLink.Auditing` is a host-agnostic audit library for .NET applications.
It provides the event model, read contracts, write buffer, writer, repository
port, and best-effort queue port. It does not select a database, message broker,
authorization model, HTTP endpoint, or background worker.

## Reference the library

Use a project reference while developing in this repository:

```xml
<ProjectReference Include="..\shared\ShortenLink.Auditing\ShortenLink.Auditing.csproj" />
```

For a packaged consumer:

```powershell
dotnet add package ShortenLink.Auditing
```

## Register the application services

Register one buffer per business-operation scope and a writer that uses the
host's `TimeProvider`:

```csharp
services.AddSingleton<TimeProvider>(TimeProvider.System);
services.AddScoped<AuditEventBuffer>();
services.AddScoped<AuditWriter>();
```

Record an event from application code:

```csharp
public sealed class RenameDocumentHandler(AuditWriter auditWriter)
{
    public Task HandleAsync(CancellationToken cancellationToken)
    {
        auditWriter.Record(
            actorId: "user-42",
            action: "document.renamed",
            targetType: "document",
            targetId: "doc-100",
            ownerId: "user-42");

        return Task.CompletedTask;
    }
}
```

Action and target-type catalogs belong to the consuming application. This keeps
the library independent of application permissions and domain terminology.

## Persist events

Implement `IAuditRepository` in the persistence project. Map `AuditEvent` to a
provider-specific persistence entity instead of exposing database entities to
the application.

```csharp
public sealed class AppAuditRepository(AppDbContext dbContext) : IAuditRepository
{
    public Task AddAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        // Map and add the event with the configured provider.
        throw new NotImplementedException();
    }

    public Task<AuditPage> ListAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<string>> ListActionsAsync(
        AuditReadScope readScope,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
```

`AuditReadScope.AccessibleTargetIds` is an application-provided authorization
projection. The repository must apply that scope before materializing rows.

## Deliver only after commit

Drain the scoped buffer only after the business transaction commits. Clear it
when the transaction fails:

```csharp
try
{
    var response = await unitOfWork.ExecuteAsync(operation, cancellationToken);

    foreach (var auditEvent in eventBuffer.Drain())
    {
        await auditQueue.EnqueueAsync(auditEvent, cancellationToken);
    }

    return response;
}
catch
{
    eventBuffer.Clear();
    throw;
}
```

Implement `IAuditEventQueue` with an in-memory queue, RabbitMQ, another broker,
or a direct repository adapter. For durable, business-critical audit delivery,
use a transactional outbox rather than relying only on an in-process queue.

## Host responsibilities

The consuming host owns:

- DI registration and service lifetimes;
- authorization and construction of `AuditReadScope`;
- EF Core mappings, migrations, indexes, and provider compatibility;
- queue capacity, retry/dead-letter behavior, and background workers;
- API contracts, endpoints, redaction, retention, and operational telemetry.

The ShortenLink integration is a reference implementation: application-specific
actions and target types remain in `ShortenLink.Core`, EF persistence remains in
`ShortenLink.Infrastructure`, and queue/worker registration remains in
`ShortenLink.Hosting`.
