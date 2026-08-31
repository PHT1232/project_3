namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="RequestCancellationCommand"/> — a requestor requesting cancellation
/// of an already-approved request (approval_transaction.drawio: "Bắt tín hiệu 'Request Cancel'").
/// 
/// - RequestId must be positive.
/// - RowVersion must be provided (concurrency control).
/// - Reason, if provided, must not exceed 500 chars.
/// </summary>
public class RequestCancellationCommandValidator : AbstractValidator<RequestCancellationCommand>
{
    public RequestCancellationCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0)
            .WithMessage("RequestId must be positive.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("RowVersion cannot be empty (concurrency control).");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason))
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
