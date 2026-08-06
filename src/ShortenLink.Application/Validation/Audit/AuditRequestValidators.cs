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
    }
}
