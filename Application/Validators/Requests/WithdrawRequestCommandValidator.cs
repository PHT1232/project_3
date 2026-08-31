namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="WithdrawRequestCommand"/> — a requestor withdrawing their pending request.
/// 
/// - RequestId must be positive.
/// - RowVersion must be provided (concurrency control).
/// </summary>
public class WithdrawRequestCommandValidator : AbstractValidator<WithdrawRequestCommand>
{
    public WithdrawRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0)
            .WithMessage("RequestId must be positive.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("RowVersion cannot be empty (concurrency check).");
    }
}
