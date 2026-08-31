namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="SubmitRequestCommand"/> — a requestor submitting a Pending request for approval.
/// 
/// - RequestId must be positive.
/// - RowVersion must be provided (concurrency control).
/// </summary>
public class SubmitRequestCommandValidator : AbstractValidator<SubmitRequestCommand>
{
    public SubmitRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0)
            .WithMessage("RequestId must be positive.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("RowVersion cannot be empty (concurrency check).");
    }
}
