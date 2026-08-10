namespace ShortenLink.Application.Abstractions;

/// <summary>
/// Marks commands that own their persistence transaction, such as durable
/// background-job submission which must commit before the HTTP response.
/// </summary>
public interface IBypassUnitOfWork
{
}
