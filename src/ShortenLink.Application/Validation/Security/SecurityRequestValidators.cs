using ShortenLink.Application.Features.Security.ApiKeys;
using ShortenLink.Application.Features.Security.Assignments;
using ShortenLink.Application.Features.Security.Roles;
using ShortenLink.Application.Features.Security.Users;
using ShortenLink.Core.Security;

namespace ShortenLink.Application.Validation.Security;

public sealed class CreateCurrentUserApiKeyCommandValidator
    : AbstractValidator<CreateCurrentUserApiKeyCommand>
{
    public CreateCurrentUserApiKeyCommandValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .WithName("displayName")
            .WithMessage("API key display name is required.")
            .WithErrorCode(ErrorCodes.InvalidApiKey);
    }
}

public sealed class RenameCurrentUserApiKeyCommandValidator
    : AbstractValidator<RenameCurrentUserApiKeyCommand>
{
    public RenameCurrentUserApiKeyCommandValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .WithName("displayName")
            .WithMessage("API key display name is required.")
            .WithErrorCode(ErrorCodes.InvalidApiKey);
    }
}

public sealed class DisableSecurityAssignmentCommandValidator
    : AbstractValidator<DisableSecurityAssignmentCommand>
{
    public DisableSecurityAssignmentCommandValidator()
    {
        RuleFor(request => request.CredentialKeyHash)
            .Matches("^[0-9a-fA-F]{64}$")
            .WithName("credentialKeyHash")
            .WithMessage("Credential key hash is invalid.")
            .WithErrorCode(ErrorCodes.InvalidCredentialHash);
    }
}

public sealed class UpsertSecurityAssignmentCommandValidator
    : AbstractValidator<UpsertSecurityAssignmentCommand>
{
    public UpsertSecurityAssignmentCommandValidator()
    {
        RuleFor(request => request.CredentialKey)
            .NotEmpty()
            .WithName("credentialKey")
            .WithMessage("Credential key is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityAssignment);
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithName("name")
            .WithMessage("Assignment name is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityAssignment);
        RuleForEach(request => request.Roles ?? Array.Empty<string>())
            .Must(ShortenLinkSystemRoles.PermissionBundles.ContainsKey)
            .WithName("roles")
            .WithMessage("Unknown system role '{PropertyValue}'.")
            .WithErrorCode(ErrorCodes.InvalidRole);
        RuleForEach(request => request.Permissions ?? Array.Empty<string>())
            .Must(ShortenLinkPermissionCatalog.All.Contains)
            .WithName("permissions")
            .WithMessage("Unknown permission '{PropertyValue}'.")
            .WithErrorCode(ErrorCodes.InvalidPermission);
    }
}

public sealed class UpsertCustomSecurityRoleCommandValidator
    : AbstractValidator<UpsertCustomSecurityRoleCommand>
{
    public UpsertCustomSecurityRoleCommandValidator()
    {
        RuleFor(request => request.Id)
            .NotEmpty()
            .WithName("id")
            .WithMessage("Custom role id is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityRole);
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithName("name")
            .WithMessage("Custom role name is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityRole);
        RuleForEach(request => request.Permissions ?? Array.Empty<string>())
            .Must(ShortenLinkPermissionCatalog.All.Contains)
            .WithName("permissions")
            .WithMessage("Unknown permission '{PropertyValue}'.")
            .WithErrorCode(ErrorCodes.InvalidPermission);
    }
}

public sealed class UpsertSecurityUserCommandValidator
    : AbstractValidator<UpsertSecurityUserCommand>
{
    public UpsertSecurityUserCommandValidator()
    {
        RuleFor(request => request.Id)
            .NotEmpty()
            .WithName("id")
            .WithMessage("User id is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityUser);
        RuleFor(request => request.Username)
            .NotEmpty()
            .WithName("username")
            .WithMessage("Username is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityUser);
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .WithName("displayName")
            .WithMessage("Display name is required.")
            .WithErrorCode(ErrorCodes.InvalidSecurityUser);
    }
}
