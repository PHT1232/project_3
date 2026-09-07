namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="ApproveWithdrawalCommand"/> — an approver's response to a withdrawal
/// request (team decision 2026-09-07: withdrawal is two-step).
///
/// - RequestId must be positive.
/// - RowVersion cannot be empty (concurrency check).
/// - Reason, if provided, must not exceed 500 chars.
///
/// Deliberately identical to <see cref="ApproveCancellationCommandValidator"/>: the two decisions
/// carry the same payload, so they get the same rules.
/// </summary>
public class ApproveWithdrawalCommandValidator : AbstractValidator<ApproveWithdrawalCommand>
{
    public ApproveWithdrawalCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0)
            .WithMessage("RequestId must be positive.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("RowVersion cannot be empty (concurrency check).");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Reason))
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
