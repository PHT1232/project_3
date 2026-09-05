using Application.DTOs.Requests;
using Application.Validators.Requests;
using FluentAssertions;

namespace Application.UnitTests.Requests;

/// <summary>
/// Plan §3.6 guards the Pending -> Rejected edge with "Comment required". The guard was never
/// implemented: an approver could reject a whole request and leave the requestor with no reason,
/// and <c>Request.DecisionComment</c> stored null (revision-3 finding M5).
///
/// Written from the Plan's transition table, not from the validator: the table puts the guard on
/// Pending -> Rejected only, so approvals and partial approvals must stay comment-optional. A
/// test that required a comment everywhere would pass against a stricter-than-specified
/// implementation, which is the mistake the previous audit called out.
/// </summary>
public class ApproveRequestCommandValidatorTests
{
    private readonly ApproveRequestCommandValidator validator = new();

    private static ApproveRequestCommand Command(string?[] decisions, string? comment) =>
        new(
            RequestId: 1,
            RowVersion: Guid.NewGuid(),
            LineDecisions: decisions
                .Select((d, index) => new LineDecision(index + 1, d!, d == "modified" ? 2 : null))
                .ToList(),
            Comment: comment);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejecting_every_line_without_a_comment_is_refused(string? comment)
    {
        var result = validator.Validate(Command(["rejected", "rejected"], comment));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(ApproveRequestCommand.Comment)
            && e.ErrorMessage == "A comment is required when rejecting a request.");
    }

    [Fact]
    public void Rejecting_every_line_with_a_comment_is_accepted()
    {
        validator.Validate(Command(["rejected", "rejected"], "Over budget this month."))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_single_rejected_line_request_still_needs_a_comment()
    {
        // One line, rejected -> the whole request is Rejected, so the guard applies.
        validator.Validate(Command(["rejected"], null)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("approved", "approved")]  // -> Approved
    [InlineData("approved", "rejected")]  // -> PartiallyApproved
    [InlineData("modified", "rejected")]  // -> PartiallyApproved
    [InlineData("modified", "modified")]  // -> PartiallyApproved (reduced quantities)
    public void Any_outcome_other_than_outright_rejection_leaves_the_comment_optional(
        string first, string second)
    {
        validator.Validate(Command([first, second], comment: null))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Decision_casing_does_not_defeat_the_guard()
    {
        validator.Validate(Command(["REJECTED", "Rejected"], comment: null))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_comment_length_cap_still_applies_to_a_rejection()
    {
        var result = validator.Validate(Command(["rejected"], new string('x', 1001)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Comment must not exceed 1000 characters.");
    }
}
