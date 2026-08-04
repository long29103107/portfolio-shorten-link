namespace ShortenLink.Core.Abstractions;

public interface IShortLinkClickRecorder
{
    Task RecordAsync(RecordShortLinkClickRequest request, CancellationToken cancellationToken = default);
}
