using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators.Users;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Matches(@"^[\p{L}\p{M} .'-]{1,15}$");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(25).EmailAddress();
        RuleFor(x => x.Role).NotEmpty();
        RuleFor(x => x.SuperiorEmployeeNumber).InclusiveBetween(0, 1000);
    }
}
