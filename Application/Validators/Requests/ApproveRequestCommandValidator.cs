namespace Application.Validators.Requests;

using Application.DTOs.Requests;
using FluentValidation;

/// <summary>
/// Validates <see cref="ApproveRequestCommand"/> — an approver's decision to approve/reject/
/// partially approve a request (approval_transaction.drawio: "Kiểm tra" → status change).
///
/// - RequestId must be positive.
/// - LineDecisions must not be empty.
/// - Each decision must be one of: 'approved', 'rejected', 'modified'.
/// - If decision is 'modified', ModifiedQuantity must be > 0.
/// - Comment, if provided, must not exceed 1000 chars.
/// - Overall result: if all lines approved → Approved, some approved → PartiallyApproved, all rejected → Rejected.
/// </summary>
public class ApproveRequestCommandValidator : AbstractValidator<ApproveRequestCommand>
{
    public ApproveRequestCommandValidator()
    {
        RuleFor(x => x.RequestId)
            .GreaterThan(0)
            .WithMessage("RequestId must be positive.");

        RuleFor(x => x.RowVersion)
            .NotEmpty()
            .WithMessage("RowVersion cannot be empty (concurrency check).");

        RuleFor(x => x.LineDecisions)
            .NotEmpty()
            .WithMessage("At least one line decision is required.");

        RuleForEach(x => x.LineDecisions)
            .ChildRules(line =>
            {
                line.RuleFor(d => d.RequestItemId)
                    .GreaterThan(0)
                    .WithMessage("RequestItemId must be positive.");

                line.RuleFor(d => d.Decision)
                    .NotEmpty()
                    .Must(d => new[] { "approved", "rejected", "modified" }.Contains(d.ToLower()))
                    .WithMessage("Decision must be 'approved', 'rejected', or 'modified'.");

                line.RuleFor(d => d.ModifiedQuantity)
                    .GreaterThan(0)
                    .When(d => d.Decision.Equals("modified", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("ModifiedQuantity must be > 0 when decision is 'modified'.");

                line.RuleFor(d => d.ModifiedQuantity)
                    .Null()
                    .When(d => !d.Decision.Equals("modified", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("ModifiedQuantity must be null unless decision is 'modified'.");
            });

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.Comment))
            .WithMessage("Comment must not exceed 1000 characters.");
    }
}
