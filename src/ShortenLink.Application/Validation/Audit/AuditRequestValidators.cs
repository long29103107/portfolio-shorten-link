using FluentValidation;
using ShortenLink.Application.Features.Audit;

namespace ShortenLink.Application.Validation.Audit;

public sealed class ListShortLinkAuditEventsQueryValidator
    : AbstractValidator<ListShortLinkAuditEventsQuery>
{
    public ListShortLinkAuditEventsQueryValidator()
    {
        RuleFor(query => query.Params)
            .NotNull()
            .WithMessage("The audit filter is required.")
            .WithErrorCode(ErrorCodes.InvalidFilter);

        RuleFor(query => query.Params)
            .Must(static request => request.From is null
                || request.To is null
                || request.From <= request.To)
            .When(static query => query.Params is not null)
            .WithName("from")
            .WithMessage("The audit time range is invalid.")
            .WithErrorCode(ErrorCodes.InvalidFilter);
    }
}
