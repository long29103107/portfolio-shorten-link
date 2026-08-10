using System.Diagnostics.Metrics;

namespace ShortenLink.Hosting;

public static class BulkJobMetrics
{
    private static readonly Meter Meter = new("ShortenLink.BulkJobs", "1.0");
    public static readonly Counter<long> Submitted = Meter.CreateCounter<long>("shortenlink.bulk_jobs.submitted");
    public static readonly Counter<long> Completed = Meter.CreateCounter<long>("shortenlink.bulk_jobs.completed");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("shortenlink.bulk_jobs.failed");
    public static readonly Counter<long> Cancelled = Meter.CreateCounter<long>("shortenlink.bulk_jobs.cancelled");
    public static readonly Counter<long> Retried = Meter.CreateCounter<long>("shortenlink.bulk_jobs.retried");
    public static readonly UpDownCounter<long> QueueDepth = Meter.CreateUpDownCounter<long>("shortenlink.bulk_jobs.queue_depth");
}
