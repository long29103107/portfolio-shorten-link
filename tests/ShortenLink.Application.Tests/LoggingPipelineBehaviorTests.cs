using ShortenLink.Application.Behaviors;
using ShortenLink.Application.Diagnostics;
using ShortenLink.Mediator;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class LoggingPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_ReportsCompletionThroughStructuredLogger()
    {
        var logger = new CaptureLogger();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger);

        var response = await behavior.Handle(
            new TestRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", response);
        var completed = Assert.Single(logger.Completed);
        Assert.Equal(nameof(TestRequest), completed.RequestName);
        Assert.True(completed.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task Handle_ReportsFailureTypeWithoutExceptionPayloadAndRethrows()
    {
        var logger = new CaptureLogger();
        var behavior = new LoggingPipelineBehavior<TestRequest, string>(logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new TestRequest(),
                () => throw new InvalidOperationException("do not log this payload"),
                CancellationToken.None));

        var failed = Assert.Single(logger.Failed);
        Assert.Equal(nameof(TestRequest), failed.RequestName);
        Assert.Equal(typeof(InvalidOperationException), failed.ExceptionType);
        Assert.True(failed.ElapsedMilliseconds >= 0);
    }

    private sealed record TestRequest : IRequest<string>;

    private sealed class CaptureLogger : IRequestLogger
    {
        public List<(string RequestName, long ElapsedMilliseconds)> Completed { get; } = [];
        public List<(string RequestName, long ElapsedMilliseconds, Type ExceptionType)> Failed { get; } = [];

        public void RequestCompleted(string requestName, long elapsedMilliseconds)
            => Completed.Add((requestName, elapsedMilliseconds));

        public void RequestFailed(string requestName, long elapsedMilliseconds, Type exceptionType)
            => Failed.Add((requestName, elapsedMilliseconds, exceptionType));
    }
}
