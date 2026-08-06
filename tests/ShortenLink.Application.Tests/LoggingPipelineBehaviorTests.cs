using System.Diagnostics;
using System.Text;
using ShortenLink.Application.Behaviors;
using ShortenLink.Mediator;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class LoggingPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_PreservesExistingTraceDiagnosticMessages()
    {
        var listener = new BufferTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            var behavior = new LoggingPipelineBehavior<TestRequest, string>();

            var response = await behavior.Handle(
                new TestRequest(),
                () => Task.FromResult("ok"),
                CancellationToken.None);

            Assert.Equal("ok", response);
            Assert.Contains(
                "ShortenLinkRequestCompleted request=TestRequest elapsed_ms=",
                listener.Text);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    [Fact]
    public async Task Handle_PreservesFailureDiagnosticAndRethrows()
    {
        var listener = new BufferTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            var behavior = new LoggingPipelineBehavior<TestRequest, string>();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                behavior.Handle(
                    new TestRequest(),
                    () => throw new InvalidOperationException("do not log this payload"),
                    CancellationToken.None));

            Assert.Contains(
                "ShortenLinkRequestFailed request=TestRequest elapsed_ms=",
                listener.Text);
            Assert.Contains("exception_type=InvalidOperationException", listener.Text);
            Assert.DoesNotContain("do not log this payload", listener.Text);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            listener.Dispose();
        }
    }

    private sealed record TestRequest : IRequest<string>;

    private sealed class BufferTraceListener : TraceListener
    {
        private readonly StringBuilder buffer = new();

        public string Text => buffer.ToString();

        public override void Write(string? message) => buffer.Append(message);

        public override void WriteLine(string? message) => buffer.AppendLine(message);
    }
}
