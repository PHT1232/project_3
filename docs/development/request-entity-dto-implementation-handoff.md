# Stationery Request Entity & DTO Implementation

**Date:** 2026-08-30  
**Scope:** Core entities, Application DTOs, validators, and service interfaces for the stationery request lifecycle (Plan M3/M4, §3.4–§4.2).

---

## What Was Built

### Core Entities

Three new entities in `Core/Entities/`:

1. **`Request.cs`** — Header of a stationery request.
   - Requestor: FK to `ApplicationUser.Id` (employee number).
   - Approver: FK to `ApplicationUser.Id` (employee number), nullable.
   - Status: one of Pending, Submitted, Approved, PartiallyApproved, Rejected, Withdrawn, CancellationPending, Fulfilled (Plan §3.6).
   - `TotalEstimatedCost`: snapshotted at submission (CLAUDE.md principle #8).
   - `RequiredByDate`: delivery deadline.
   - `DecisionComment`: approver's reason.
   - `RowVersion`: Guid-based optimistic concurrency token.
   - Navigation: `Items` (RequestItem list), `StatusHistory` (RequestStatusHistory list).

2. **`RequestItem.cs`** — Line item in a request.
   - ItemId: FK to StationeryItem.
   - Quantity: > 0.
   - `UnitCostSnapshot`: item's price at submission; price changes don't affect history.
   - `LineTotal`: Quantity × UnitCostSnapshot (stored, not computed).

3. **`RequestStatusHistory.cs`** — Audit trail for status transitions.
   - Tracks all state changes (Submitted, Approved, Rejected, Withdrawn, etc.).
   - `FromStatus` (nullable for initial "created" event), `ToStatus`.
   - Actor: FK to ApplicationUser (who made the transition).
   - `Comment`: justification (reason for approval/rejection).
   - Append-only; never deleted.

### Application DTOs

Five new files in `Application/DTOs/Requests/`:

1. **`CreateRequestCommand.cs`** — Requestor creates a new request.
   - Input: list of (ItemId, Quantity) pairs + optional RequiredByDate.
   - Server-side: resolves approver from requestor's superior, snapshots unit costs, computes line totals.

2. **`RequestDto.cs`** — Display model (+3 sub-DTOs):
   - `RequestDto`: full request header, items, and history.
   - `RequestItemDto`: line item with item name, supplier, category (enriched for UI).
   - `RequestStatusHistoryDto`: status change event with actor name and timestamp.

3. **`ApproveRequestCommand.cs`** — Approver's decision (approve/reject/partially approve).
   - Input: list of per-line decisions ('approved', 'rejected', 'modified') + optional changed quantities.
   - Server-side: computes overall request status (Approved/PartiallyApproved/Rejected) and creates stock movements (M4).

4. **`ApproveCancellationCommand.cs`** — Approver responds to a cancellation request.
   - Input: RequestId, RowVersion, Approved (bool), optional Reason.
   - If Approved = true: CancellationPending → Cancelled (stock reversal M4+).
   - If Approved = false: deny cancellation, stay in Approved/PartiallyApproved.

### Validators

Three in `Application/Validators/Requests/`:

1. **`CreateRequestCommandValidator.cs`**
   - Items list not empty; no duplicates by ItemId.
   - Each quantity > 0 and < 10000.
   - RequiredByDate is optional (no future-date validation).

2. **`ApproveRequestCommandValidator.cs`**
   - RequestId > 0, RowVersion not empty.
   - LineDecisions not empty.
   - Each decision is 'approved', 'rejected', or 'modified'.
   - ModifiedQuantity required for 'modified', must be > 0.
   - Comment max 1000 chars.

3. **`ApproveCancellationCommandValidator.cs`**
   - RequestId > 0, RowVersion not empty.
   - Reason (optional) max 500 chars.

### Service Interfaces

Two in `Application/Interfaces/Requests/`:

1. **`IRequestService.cs`** — State machine & business logic (from approval_transaction.drawio).
   - `CreateAsync`: new Pending request.
   - `SubmitAsync`: Pending → Pending (marks as submitted, notifies approver + requestor).
   - `ApproveAsync`: Pending → Approved/PartiallyApproved/Rejected (with per-line decision support).
   - `WithdrawAsync`: Pending → Withdrawn (requestor only).
   - `RequestCancellationAsync`: Approved/PartiallyApproved → CancellationPending (M4).
   - `ApproveCancellationAsync`: CancellationPending → Cancelled (if approved) or back to Approved (if denied).
   - `DeletePendingAsync`: delete Pending requests only.
   - All transactional; all include concurrency checks (RowVersion compare-then-set).
   - **Key flow from diagram:** Pending → (check) → Approved/Rejected → (if Approved) → request cancel → CancellationPending → (check) → Cancelled/back to Approved.

2. **`IRequestQueries.cs`** — Read-side queries with role-based visibility.
   - `GetVisibleAsync`: paginated list (requestor's own, pending approvals, or all if Manager+).
   - `GetByIdAsync`: single request + full history.
   - `GetPendingApprovalsAsync`: Pending statuses awaiting caller's decision.
   - `GetByRequestorAsync`: requests by one employee (filtered by visibility).
   - `GetStatusSummaryForDashboardAsync`: count by status for the dashboard widget.

---

## Architecture & Design Decisions

### Core Principles Applied

- **CLAUDE.md principle #5 (Stock as ledger):** Stock moves only on approval via `IStockService.IssueAsync` (M4); creating/submitting a request does **not** touch inventory.
- **CLAUDE.md principle #6 (Atomic state changes):** Status transitions, history rows, and notifications commit or roll back together (one `SaveChangesAsync` per decision).
- **CLAUDE.md principle #8 (Cost snapshots):** Unit cost is frozen at submission; catalogue price changes never rewrite history.
- **CLAUDE.md principle #4 (Concurrency):** Optimistic locking via `Guid RowVersion` (app-managed), not SQL Server `rowversion`. Allows same logic to run on SQLite tests.
- **CLAUDE.md principle #9 (Authorization):** Service methods check row ownership (requestor can only see their own, approvers see submissions from their subordinates).

### Status Enum

Per approval_transaction.drawio diagram flow:
- **Pending**: created and ready for approval (first state after creation).
- **Approved**: approver approved all lines; stock moves (M4).
- **PartiallyApproved**: approver approved some, rejected others; stock moves for approved lines only.
- **Rejected**: approver rejected all lines; no stock moves.
- **Withdrawn**: requestor withdrew before approval.
- **CancellationPending**: requestor requested cancellation of an approved order (diagram flow: "Bắt tín hiệu 'Request Cancel Approve?'").
- **Cancelled**: approver approved the cancellation; stock reversal (M4+).
- **Fulfilled**: all lines fulfilled (stock already moved); M4+.

### Partial Approval Design

Plan §3.6 and §4.2 specify: approver can approve each line separately, reducing quantity if needed.

Example workflow:
1. Requestor submits 100x Pens + 50x Notebooks.
2. Approver: "Approve 80 Pens (too many), reject Notebooks (budget)."
3. Status becomes PartiallyApproved.
4. IStockService.IssueAsync moves 80x Pens; Notebooks remain untouched.
5. Dashboard shows request as "Partially Fulfilled" (waiting for requestor to follow up or withdraw).

---

## What Was NOT Built

- **EF Core Configurations** — no `Infrastructure/Data/Configurations/Request*.cs` yet.
- **Migration** — no `Infrastructure/Data/Migrations/*_Requests` yet.
- **Database seeder** — no `DbSeeder` for requests and status history yet.
- **Service implementations** — `IRequestService` and `IRequestQueries` are interfaces only.
- **Controllers** — no `WebApi/Controllers/RequestsController.cs` yet.
- **Frontend** — no React pages (New Request, My Requests, Approvals, etc.) yet.
- **Tests** — no unit/integration tests yet.
- **Stock movement service** — `IStockService.IssueAsync` is already scaffolded but not called by request approval (M4 feature).

---

## Files Created

```
Core/Entities/
  Request.cs
  RequestItem.cs
  RequestStatusHistory.cs

Application/DTOs/Requests/
  CreateRequestCommand.cs
  RequestDto.cs
  ApproveRequestCommand.cs

Application/Validators/Requests/
  CreateRequestCommandValidator.cs
  ApproveRequestCommandValidator.cs

Application/Interfaces/Requests/
  IRequestService.cs
  IRequestQueries.cs
```

---

## Build & Validation

- ✅ `dotnet build Project.slnx --nologo` — **0 errors, 2 unrelated warnings** (SQLitePCLRaw advisory).
- ✅ All new files compile cleanly.
- ✅ No schema or test code was written, so no migration/test run yet.

---

## Next Steps

1. **Infrastructure layer (M3 part 1):**
   - EF Core configurations for Request, RequestItem, RequestStatusHistory.
   - Add `DbSet`s to DataContext.
   - Create `Infrastructure/Data/Migrations/*_Requests`.

2. **Infrastructure layer (M3 part 2):**
   - Implement `IRequestService` in `Infrastructure/Services/RequestService.cs`.
   - Implement `IRequestQueries` in `Infrastructure/Queries/RequestQueries.cs`.
   - Wire `RequestStatusHistory` writes (audit trail).
   - Hook notification triggers (Plan §3.5 events 2, 3, 4, 6).

3. **WebApi layer (M3 part 3):**
   - `WebApi/Controllers/RequestsController.cs`:
     - `POST /api/v1/requests` — create.
     - `POST /api/v1/requests/{id}/submit` — submit.
     - `POST /api/v1/requests/{id}/approve` — decide.
     - `POST /api/v1/requests/{id}/withdraw` — withdraw.
     - `GET /api/v1/requests` — list.
     - `GET /api/v1/requests/{id}` — detail.
     - `GET /api/v1/requests/pending` — for approvers.
   - Add `RequireManager`/`RequireApprover` policies + row-level ownership checks.
   - Wire validators (`FluentValidation` middleware).

4. **Frontend (M3 part 4):**
   - `src/api/requests.js` — API client calls.
   - `src/pages/requests/NewRequestPage.jsx` — form for creating requests.
   - `src/pages/requests/MyRequestsPage.jsx` — requestor's dashboard.
   - `src/pages/requests/ApprovalsPage.jsx` — approver's pending-approvals inbox.
   - `src/components/RequestDetail.jsx` — request display with history.

5. **Tests (M3 part 5):**
   - Unit tests for create/submit/approve logic and validators.
   - Integration tests for the full workflow (create → submit → approve → stock move check).
   - Frontend tests for form submission and approval modals.

6. **Schema Migration & Database:**
   - Apply the migration to a real SQL Server instance.
   - Smoke test: create a request, submit it, approve it, verify stock and history.

---

## Known Open Questions

1. **Partial approval UX:** Should the UI show a "(2/3 approved, 1/3 rejected)" badge on PartiallyApproved requests?
2. **Withdrawal → resubmit:** Can a withdrawn request be re-opened? Currently status is terminal to Fulfilled.
3. **Request expiration:** Should old Rejected/Withdrawn requests auto-archive after 90 days?
4. **Notification delivery:** How should notifications be sent? Email (M5+), in-app polling (Plan §3.4 note), or WebSocket (not in Plan)?

---

## Assumptions Made & Flagged

- Diagram flow is from approval_transaction.drawio (Quang's workflow diagram), not the Plan's original state machine — they align on key statuses (Approved/Rejected/Withdrawn) but differ on initial state (Pending vs Draft).
- Partial approval allows approver to reduce quantity but not increase it (conservative).
- ModifiedQuantity in LineDecision is only allowed when decision is 'modified', not for 'approved' or 'rejected'.
- Status is text (string), not an enum column, to allow new statuses to be added without migration (Plan pattern for flexibility).
- Cancellation requests ($ApproveCancellationCommand) are a separate flow and may be extended with more complex rules (e.g., time limits on when cancellation is allowed).

---

## Testing Approach (for M3 PR review)

Once the service and controller are implemented:

```pow
# Backend tests
dotnet test Tests/Application.UnitTests/ --filter "*Request*" -v normal
dotnet test Tests/WebApi.IntegrationTests --filter "*Request*" -v normal

# Frontend tests (after pages written)
npm test -- --run src/pages/requests/
```

---

## Documentation Checklist

- ✅ Entities documented with design intent (stock, concurrency, snapshots).
- ✅ DTOs documented with field meanings and validation rules.
- ✅ Validators documented with rule explanations.
- ✅ Service interfaces documented with transactional guarantees.
- ✅ This handoff file created.
- ⏳ EF Core Configurations, Service implementations, Controllers, Frontend, Tests — to follow in M3.

