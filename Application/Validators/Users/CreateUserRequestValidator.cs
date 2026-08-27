using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators.Users;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.EmployeeNumber).InclusiveBetween(1, 1000);
        RuleFor(x => x.Name).NotEmpty().Matches(@"^[\p{L}\p{M} .'-]{1,15}$");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(25).EmailAddress();
        RuleFor(x => x.Role).NotEmpty();
        RuleFor(x => x.SuperiorEmployeeNumber).InclusiveBetween(0, 1000);

        // Proposed default (matches change-password policy) — not explicitly specified by the
        // Plan; confirm before relying on it. See implementation-plan.md §7.
        RuleFor(x => x.InitialPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
