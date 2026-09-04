using Application.DTOs.Auth;
using FluentValidation;

namespace Application.Validators.Auth;

/// <summary>
/// Shape-only validation for <see cref="LoginRequest"/>: is this request answerable at all?
///
/// It deliberately does NOT judge whether the identifier could exist — no employee-number range
/// check, no domain check. An identifier that cannot match an account must fail the same way as
/// a wrong password (generic 401 from the account store), because a 400 here would tell an
/// attacker that some identifiers are worth trying and others are not (Plan §9.2, and the
/// Login_UnknownEmployeeNumber_ReturnsSameGeneric401AsWrongPassword contract test).
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.EmployeeNumber.HasValue ^ !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Provide either an employee number or an email address, not both.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
