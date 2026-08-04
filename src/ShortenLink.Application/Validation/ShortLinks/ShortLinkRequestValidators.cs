using ShortenLink.Application.Features.ShortLinks.Expiration;
using ShortenLink.Application.Features.ShortLinks.Shares;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;

namespace ShortenLink.Application.Validation.ShortLinks;

public sealed class ExecuteShortLinkExpirationCommandValidator
    : AbstractValidator<ExecuteShortLinkExpirationCommand>
{
    public ExecuteShortLinkExpirationCommandValidator()
    {
        RuleFor(request => request.RetainExpiredForSeconds)
            .GreaterThanOrEqualTo(0)
            .When(request => request.RetainExpiredForSeconds.HasValue)
            .WithName("retainExpiredForSeconds")
            .WithMessage("Retention duration cannot be negative.")
            .WithErrorCode(ShortLinkErrorCodes.InvalidExpiration);
    }
}

public sealed class UpsertShortLinkShareCommandValidator
    : AbstractValidator<UpsertShortLinkShareCommand>
{
    public UpsertShortLinkShareCommandValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty()
            .WithName("username")
            .WithMessage("Choose a user and View or Edit access.")
            .WithErrorCode(ErrorCodes.InvalidShare);
        RuleFor(request => request.Access)
            .Must(static access => Enum.TryParse<ShortLinkShareAccess>(access, true, out _))
            .WithName("access")
            .WithMessage("Choose a user and View or Edit access.")
            .WithErrorCode(ErrorCodes.InvalidShare);
    }
}

public sealed class SetShortLinkSharingModeCommandValidator
    : AbstractValidator<SetShortLinkSharingModeCommand>
{
    public SetShortLinkSharingModeCommandValidator()
    {
        RuleFor(request => request.Mode)
            .Must(static mode => Enum.TryParse<ShortLinkSharingMode>(mode, true, out _))
            .WithName("mode")
            .WithMessage("Choose Public or AllowList sharing.")
            .WithErrorCode(ErrorCodes.InvalidShare);
    }
}
