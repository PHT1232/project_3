# Two-step withdrawal — implementation handoff

> Branch `khang`, 2026-09-07, on top of `5bff750`. Written so another team member can explain the
> change in a PR review without reading the session it came from.
> Companion documents: [PROJECT_AUDIT.md](../../PROJECT_AUDIT.md) and the dated entry in
> [AI_usage_report.md](../../AI_usage_report.md).

---

## 1. Why this change exists

**Team decision, 2026-09-07:** *a requestor must not be able to revoke a request on their own — a
higher rank has to confirm it.*

Cancellation already worked that way. Withdrawal did not:

| Requestor action | Applies to | Before | After |
|---|---|---|---|
| **Cancel** | `Approved` / `PartiallyApproved` | two-step — approver confirms | unchanged |
| **Withdraw** | `Pending` | **one step, immediate and final** | two-step — approver confirms |

So the fix is entirely on the withdrawal side. Nothing about cancellation changed.

### This deliberately overrides Plan §3.6

The Plan's transition table says:

| From | To | Who | Guard |
|---|---|---|---|
| Pending | Withdrawn | Requestor (owner) | Status is still `Pending` |

and it warns, in bold, that treating withdraw and cancel as the same operation is one of "the two
most common failures here". **We are knowingly doing something the Plan advises against**, because
the team ruled that the business rule matters more than the Plan's wording. Recording it the way
K7 and K8 were recorded, rather than letting it look like drift, is the whole point of this
document.

The Plan's underlying distinction still holds, and the code still reflects it:

- **Withdraw** acts on a request **nobody has decided yet**. It moves **no stock**, because a
  request that was never approved never issued any.
- **Cancel** unwinds an **already-approved** request and **restores stock**.

What changed is only *who has to agree*, not what the two operations mean.

---

## 2. The new state machine

```
Draft ──submit──► Pending ──approve/reject──► Approved / PartiallyApproved / Rejected
                    │  ▲
   requestor asks   │  │  approver refuses  (back to the queue)
   to withdraw      ▼  │
              WithdrawalPending ──approver confirms──► Withdrawn   (terminal)
```

`Pending → Withdrawn` **no longer exists**. The only route to `Withdrawn` is through
`WithdrawalPending`, and only the approver can take it.

Refusal is simpler than the cancellation equivalent: a refused *cancellation* has to read the
prior status out of the audit trail, because it could have been `Approved` or `PartiallyApproved`.
`WithdrawalPending` is reachable only from `Pending`, so there is nothing to look up — refusing
always returns the request to `Pending`.

---

## 3. Files changed

### Backend

| File | Change |
|---|---|
| `Application/Services/Requests/RequestStateMachine.cs` | Added `WithdrawalPending`. Removed the `Pending → Withdrawn` edge; added `Pending → WithdrawalPending` and `WithdrawalPending → {Withdrawn, Pending}`. Doc comment records the override. |
| `Application/DTOs/Requests/WithdrawRequestCommand.cs` | Now carries an optional `Reason`, shown to the approver when they decide — same as `RequestCancellationCommand`. |
| `Application/DTOs/Requests/ApproveWithdrawalCommand.cs` | **New.** Mirrors `ApproveCancellationCommand`. |
| `Application/Validators/Requests/WithdrawRequestCommandValidator.cs` | Added the 500-char `Reason` rule. |
| `Application/Validators/Requests/ApproveWithdrawalCommandValidator.cs` | **New.** Picked up automatically by `AddValidatorsFromAssemblyContaining` — no DI change. |
| `Application/Interfaces/Requests/IRequestService.cs` | `WithdrawAsync` → `RequestWithdrawalAsync`; added `ApproveWithdrawalAsync`. |
| `Infrastructure/Services/RequestService.cs` | `RequestWithdrawalAsync` parks at `WithdrawalPending` and fires **no** notification; `ApproveWithdrawalAsync` resolves it and notifies only on the final `Withdrawn`. |
| `Infrastructure/Queries/RequestQueries.cs` | The approvals queue now also returns `WithdrawalPending`. |
| `Infrastructure/Queries/EligibilityQueries.cs` | `WithdrawalPending` added to `CommittedStatuses`. |
| `Infrastructure/Data/Configurations/RequestConfiguration.cs` | `CK_Requests_Status` gains `'WithdrawalPending'`. |
| `Infrastructure/Data/Migrations/20260907122333_AddWithdrawalPendingStatus.cs` | **New migration.** |
| `WebApi/Controllers/RequestsController.cs` | `POST /requests/{id}/withdraw` now passes the reason and calls `RequestWithdrawalAsync`. |
| `WebApi/Controllers/ApprovalController.cs` | **New** `POST /approvals/{id}/withdrawal-approval`. |

**Why `WithdrawAsync` was renamed.** Its behaviour changed fundamentally — it no longer withdraws
anything. Leaving the old name would have been exactly the kind of doc-vs-reality mismatch the
2026-09-04 audit kept finding. The **URL** `POST /requests/{id}/withdraw` was deliberately *not*
renamed: it still describes what the requestor is doing from their side, and renaming it would
have churned the SPA and the tests for no behavioural gain.

### Frontend

| File | Change |
|---|---|
| `src/api/requests.js` | `withdrawRequest` → `requestWithdrawal` (now takes a reason); added `approveWithdrawal`. |
| `src/pages/requests/components/WithdrawalModal.jsx` | **New.** Requestor's "ask to withdraw" modal — replaces a bare `window.confirm()`, which fitted the old immediate-and-final behaviour. |
| `src/pages/requests/components/WithdrawalDecisionModal.jsx` | **New.** Approver's confirm / refuse modal, mirroring `CancellationDecisionModal`. |
| `src/pages/requests/MyRequestsPage.jsx` | Withdraw button opens the modal; success message says "Awaiting approver confirmation"; added the `WithdrawalPending` status filter. |
| `src/pages/requests/ApprovalsPage.jsx` | Third row kind with its own Decide action; empty state reworded. |
| `src/pages/requests/components/RequestStatusBadge.jsx` | Added the `WithdrawalPending` chip. |
| `src/pages/requests/components/RequestDetailModal.jsx` | Button relabelled "Request Withdrawal". |
| `src/pages/help/faqData.js` | Three answers rewritten — statuses, "how do I withdraw", and what counts against budget. |

---

## 4. API and DB changes

**New endpoint**

```
POST /api/v1/approvals/{requestId}/withdrawal-approval
{ "requestId": 30, "rowVersion": "…", "approved": true, "reason": "Agreed" }
```

`approved: true` → `Withdrawn`. `approved: false` → back to `Pending`.
Caller must be the request's approver, else **404** (not 403 — CLAUDE.md #9, don't leak
existence). Wrong status → **409**. Stale `rowVersion` → **409**.

**Changed endpoint** — `POST /api/v1/requests/{id}/withdraw` accepts an optional `reason` and now
returns a request in **`WithdrawalPending`**, not `Withdrawn`. This is a breaking behavioural
change for any caller that assumed the old response.

**Database** — one migration, `20260907122333_AddWithdrawalPendingStatus`: drops and re-adds
`CK_Requests_Status` with `'WithdrawalPending'` included. No new table, column or index. `Down()`
first runs `UPDATE [Requests] SET [Status] = 'Pending' WHERE [Status] = 'WithdrawalPending'`, or
re-creating the narrower constraint would fail on any parked row.

> ⚠️ **CLAUDE.md §5: only one open PR may contain an EF migration at a time — announce this one
> before opening the PR.**

---

## 5. Design decisions worth defending in review

1. **Budget stays committed while a withdrawal is pending.** `WithdrawalPending` was added to
   `EligibilityQueries.CommittedStatuses`. If it were not, a requestor could free their allowance
   just by *asking* to withdraw and then never being refused — spending the same budget twice.
   The allowance returns only when the approver confirms.
2. **No new notification trigger.** Plan §4.2 names exactly six, and "withdrawn" is the outcome,
   not the request for it. `RequestWithdrawn` fires from `ApproveWithdrawalAsync` on the final
   `Withdrawn` only — no notification when the withdrawal is *asked for*, and none when it is
   refused. This is precisely how the cancellation flow already behaves.
3. **A reason field was added to the withdrawal request.** Without it the approver would be
   deciding blind. The cancellation flow already had one, and the two decision modals now show the
   requestor's reason the same way.
4. **No stock handling.** A request can only reach `WithdrawalPending` from `Pending`, and stock
   moves on approval — so there is never anything to unwind. Stated explicitly in the code so the
   absence reads as deliberate rather than forgotten.

---

## 6. Tests actually run

| Command | Before | After |
|---|---|---|
| `dotnet test Project.slnx` | 243 passed | **257 passed** (117 unit + 140 integration), 0 failed |
| `npx vitest run --pool=threads` | 154 passed | **157 passed** across 25 files, 0 failed |
| `npm run build` | — | clean |

**One existing test changed meaning and was rewritten**, not deleted:
`BudgetEnforcementTests.WithdrawingARequest_ReleasesItsBudget` asserted that withdrawing freed the
allowance immediately. It now asserts the new rule end to end — asking does **not** free it, and
the approver's confirmation does. It is renamed
`WithdrawingARequest_ReleasesItsBudget_OnlyOnceTheApproverConfirms`.

New backend tests: five integration cases (parks at `WithdrawalPending`; the requestor cannot
confirm their own withdrawal → 404; approver confirms → `Withdrawn` and the row appears in their
queue; approver refuses → back to `Pending`; deciding a request that is not awaiting a withdrawal
→ 409) and an expanded state-machine matrix including a named test,
`Withdrawing_Requires_TheApproversConfirmation`, that fails if anyone re-adds the direct edge.

New frontend tests: three on `ApprovalsPage` (the row renders a Decide action rather than Review;
confirm sends `approved: true`; refuse sends `approved: false` with the comment) and a rewritten
`MyRequestsPage` case covering the modal and the "Awaiting approver confirmation" message.

### Verified live (SQLEXPRESS `StationeryManagementSystem.Dev` + the SPA)

The migration auto-applied on API start — confirmed straight from
`sys.check_constraints`, which now lists `WithdrawalPending`.

| Check | Result |
|---|---|
| Requestor asks to withdraw #29 | `WithdrawalPending` |
| **Requestor tries to confirm their own withdrawal** | **404 "You are not the approver for this request."** |
| Approver refuses | back to `Pending` |
| Requestor asks again, approver confirms | `Withdrawn` |
| Audit trail on #29 | `Draft→Pending`, `Pending→WithdrawalPending`, `WithdrawalPending→Pending`, `Pending→WithdrawalPending`, `WithdrawalPending→Withdrawn` |
| Requestor's notification feed | "Request Withdrawn" present, and only after the confirmation |
| In the browser, requestor #411 | Withdraw opens the new modal, banner reads "Withdrawal requested for #30. Awaiting approver confirmation.", row shows **Withdrawal Pending** |
| In the browser, approver #410 | #30 appears in Approvals as **Withdrawal Pending** with **Decide**; the modal shows the requestor's reason "Ordered by mistake"; confirming empties the queue and #30 ends `Withdrawn` |

**Wireframe fidelity:** no new page or navigation entry. The Approvals table gains a third row
kind reusing the existing Decide button, and My Requests reuses the existing action column — both
consistent with how cancellation already renders.

**Test data:** employees 410/411 on the dev DB were reactivated for this run and deactivated
again afterwards. Requests 29 and 30 remain as evidence. Nothing was deleted.

---

## 7. Known issues and reviewer follow-ups

- **This contradicts Plan §3.6 in writing.** The Plan should be amended (revision history, the
  §3.6 table and its state diagram), the same way K7 closed the .NET-version conflict. Until that
  happens, `docs/Diagrams/request_diagrams_v3.drawio` and the Plan still show the old single-step
  withdraw. **Not done here on purpose** — CLAUDE.md forbids editing requirements and diagrams
  unilaterally, and this is the team's call to record.
- **Any request already sitting in the database keeps its status**; the migration only widens the
  vocabulary. Nothing needed back-filling, because no row could previously be `WithdrawalPending`.
- **`ApprovalController` is `[Authorize]` only**, relying on the row-level approver check in the
  service — unchanged from how the cancellation decision endpoint already worked, and consistent
  with CLAUDE.md #9's ownership-aware rule. Worth a second look if the team ever adds a policy
  there.
- **M4 (reports policy) is still open** and untouched by this work.
