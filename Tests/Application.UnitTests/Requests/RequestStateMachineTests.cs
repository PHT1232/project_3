using Application.Exceptions;
using Application.Services.Requests;
using Core.Entities;
using FluentAssertions;

namespace Application.UnitTests.Requests;

/// <summary>
/// The Plan §3.6 transition table, typed in as a test matrix (Plan T3.2 acceptance criterion:
/// "every illegal transition throws InvalidStateTransitionException"; Plan §10: "all legal
/// transitions pass, and a representative set of illegal ones throw").
///
/// These are written from the *specification*, not from the implementation — which is the point.
/// The previous audit noted that 183 tests passed while nine logical errors sat in the code,
/// because every test had been written from what the code did.
///
/// Two statuses in the Plan's diagram are deliberately absent: ReturnedForModification is out of
/// scope by team decision (CLAUDE.md K1) and Fulfilled was removed when approval itself became
/// the stock movement (audit C8).
/// </summary>
public class RequestStateMachineTests
{
    private const int Actor = 42;

    private static Request RequestIn(string status) => new()
    {
        Id = 1,
        RequestorEmployeeNumber = 7,
        ApproverEmployeeNumber = 8,
        Status = status,
    };

    // ---- Legal transitions (Plan §3.6) ------------------------------------------------

    [Theory]
    [InlineData("Draft", "Pending")]                              // requestor submits
    [InlineData("Pending", "Approved")]                           // approver approves everything
    [InlineData("Pending", "PartiallyApproved")]                  // approver approves some
    [InlineData("Pending", "Rejected")]                           // approver rejects everything
    [InlineData("Pending", "Withdrawn")]                          // requestor withdraws
    [InlineData("Approved", "CancellationPending")]               // requestor asks to cancel
    [InlineData("PartiallyApproved", "CancellationPending")]      // ditto, partial
    [InlineData("CancellationPending", "Cancelled")]              // approver grants cancellation
    [InlineData("CancellationPending", "Approved")]               // approver refuses, reverts
    [InlineData("CancellationPending", "PartiallyApproved")]      // ditto, reverts to partial
    public void Transition_LegalEdge_MovesStatusAndWritesHistory(string from, string to)
    {
        var request = RequestIn(from);
        var originalRowVersion = request.RowVersion;

        RequestStateMachine.Transition(request, to, Actor, "because");

        request.Status.Should().Be(to);
        request.RowVersion.Should().NotBe(originalRowVersion, "a transition must rotate the concurrency token");

        request.StatusHistory.Should().ContainSingle();
        var entry = request.StatusHistory[0];
        entry.FromStatus.Should().Be(from);
        entry.ToStatus.Should().Be(to);
        entry.ActorEmployeeNumber.Should().Be(Actor);
        entry.Comment.Should().Be("because");
    }

    // ---- Illegal transitions ----------------------------------------------------------

    [Theory]
    // A Draft is invisible to the approver — it cannot be decided, withdrawn or cancelled.
    [InlineData("Draft", "Approved")]
    [InlineData("Draft", "Rejected")]
    [InlineData("Draft", "Withdrawn")]
    [InlineData("Draft", "Cancelled")]
    // Pending cannot skip the approver's decision, nor go back to Draft.
    [InlineData("Pending", "Draft")]
    [InlineData("Pending", "CancellationPending")]
    [InlineData("Pending", "Cancelled")]
    // An approved request is cancelled through the two-step flow, never directly.
    [InlineData("Approved", "Cancelled")]
    [InlineData("Approved", "Withdrawn")]
    [InlineData("Approved", "Rejected")]
    [InlineData("PartiallyApproved", "Cancelled")]
    // Terminal states are terminal.
    [InlineData("Rejected", "Approved")]
    [InlineData("Rejected", "Pending")]
    [InlineData("Withdrawn", "Pending")]
    [InlineData("Cancelled", "Approved")]
    // A cancellation decision cannot invent an outcome that was never the prior state.
    [InlineData("CancellationPending", "Rejected")]
    [InlineData("CancellationPending", "Withdrawn")]
    public void Transition_IllegalEdge_Throws_AndLeavesRequestUntouched(string from, string to)
    {
        var request = RequestIn(from);
        var originalRowVersion = request.RowVersion;

        var act = () => RequestStateMachine.Transition(request, to, Actor);

        act.Should().Throw<InvalidStateTransitionException>();

        // The guard must not half-apply: an illegal call leaves no trace.
        request.Status.Should().Be(from);
        request.RowVersion.Should().Be(originalRowVersion);
        request.StatusHistory.Should().BeEmpty();
    }

    [Fact]
    public void Transition_IllegalEdge_MapsTo409()
    {
        var act = () => RequestStateMachine.Transition(RequestIn("Draft"), "Approved", Actor);

        // ConflictException is what ExceptionHandlingMiddleware turns into 409, so deriving from
        // it is load-bearing, not cosmetic.
        act.Should().Throw<InvalidStateTransitionException>()
            .Which.Should().BeAssignableTo<ConflictException>();
    }

    [Fact]
    public void Transition_UnknownCurrentStatus_Throws()
    {
        var act = () => RequestStateMachine.Transition(RequestIn("Fulfilled"), "Cancelled", Actor);

        act.Should().Throw<InvalidStateTransitionException>()
            .WithMessage("*not a known status*");
    }

    [Fact]
    public void Transition_SameStatus_IsNotLegal()
    {
        // Guards against the pre-audit bug where submit was a Pending -> Pending no-op (C1).
        var act = () => RequestStateMachine.Transition(RequestIn("Pending"), "Pending", Actor);

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void NextStatuses_TerminalState_IsEmpty()
    {
        RequestStateMachine.NextStatuses("Cancelled").Should().BeEmpty();
        RequestStateMachine.NextStatuses("Rejected").Should().BeEmpty();
        RequestStateMachine.NextStatuses("Withdrawn").Should().BeEmpty();
    }

    [Fact]
    public void Transition_AppendsToExistingHistory_RatherThanReplacingIt()
    {
        var request = RequestIn("Draft");

        RequestStateMachine.Transition(request, "Pending", Actor, "submitted");
        RequestStateMachine.Transition(request, "Approved", 99, "approved");

        request.StatusHistory.Should().HaveCount(2);
        request.StatusHistory[1].FromStatus.Should().Be("Pending");
        request.StatusHistory[1].ToStatus.Should().Be("Approved");
        request.Status.Should().Be("Approved");
    }
}
