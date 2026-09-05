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
/// - Comment is REQUIRED when every line is rejected — Plan §3.6 guards Pending -> Rejected
///   with "Comment required". Optional for approvals and partial approvals.
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

        // Plan §3.6 states the guard on Pending -> Rejected as "Comment required". It was never
        // enforced: an approver could reject an entire request and leave the requestor with no
        // reason at all, and Request.DecisionComment stored null.
        //
        // The guard fires only on an outright rejection, exactly as the Plan scopes it. The
        // header status is derived in RequestService.ApproveAsync as "every line rejected ->
        // Rejected", so that same condition is decidable here, before the service runs. A
        // PartiallyApproved outcome (a rejected line, or a reduced quantity) is a different
        // transition and the Plan puts no comment guard on it, so it stays optional there.
        RuleFor(x => x.Comment)
            .NotEmpty()
            .When(IsOutrightRejection)
            .WithMessage("A comment is required when rejecting a request.");
    }

    /// <summary>
    /// True when every line is rejected, which is what makes the request as a whole take the
    /// Pending -> Rejected edge (Plan §3.6). Mirrors the header-status rule in
    /// RequestService.ApproveAsync; an empty decision list is already refused above.
    /// </summary>
    private static bool IsOutrightRejection(ApproveRequestCommand command) =>
        command.LineDecisions is { Count: > 0 }
        && command.LineDecisions.All(d =>
            string.Equals(d.Decision, "rejected", StringComparison.OrdinalIgnoreCase));
}
