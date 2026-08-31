namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="ApproveCancellationCommand"/> — an approver's response to a
/// cancellation request (approval_transaction.drawio: "Bắt tín hiệu 'Request Cancel Approve?'").
///
/// - RequestId must be positive.
/// - RowVersion cannot be empty (concurrency check).
/// - Reason, if provided, must not exceed 500 chars.
/// </summary>
public class ApproveCancellationCommandValidator : AbstractValidator<ApproveCancellationCommand>
{
    public ApproveCancellationCommandValidator()
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
