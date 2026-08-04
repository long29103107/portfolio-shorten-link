using FluentValidation;
using ShortenLink.Application.Behaviors;
using ShortenLink.Core.Exceptions;
using ShortenLink.Mediator;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_ThrowsRequestValidationExceptionWithFieldErrors()
    {
        var behavior = new ValidationPipelineBehavior<TestRequest, string>(
            [new TestRequestValidator()]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            behavior.Handle(
                new TestRequest(string.Empty),
                () =>
                {
                    nextCalled = true;
                    return Task.FromResult("ok");
                },
                CancellationToken.None));

        Assert.False(nextCalled);
        Assert.Equal(ErrorCodes.InvalidRequest, exception.ErrorCode);
        Assert.Equal(["Value is required."], exception.Errors["value"]);
    }

    [Fact]
    public async Task Handle_ContinuesWhenRequestIsValid()
    {
        var behavior = new ValidationPipelineBehavior<TestRequest, string>(
            [new TestRequestValidator()]);

        var response = await behavior.Handle(
            new TestRequest("valid"),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", response);
    }

    private sealed record TestRequest(string Value) : IRequest<string>;

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(request => request.Value)
                .NotEmpty()
                .WithName("value")
                .WithMessage("Value is required.")
                .WithErrorCode(ErrorCodes.InvalidRequest);
        }
    }
}
