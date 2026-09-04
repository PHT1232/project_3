# Handoff — Stock movement on approval (audit finding C8)

**Date:** 2026-09-05 · **Branch:** `fix/C8-stock-on-approval` (stacked on
`fix/C7-budget-enforcement`) · **Migration in this change:**
`20260904043931_AddStockIssueOnApproval` — announce it, one open migration PR at a time.

## 1. What was wrong

Plan §3.6 defines two stock-moving transitions:

| From | To | Guard | Effect |
|---|---|---|---|
| `Pending` | `Approved` | Stock ≥ quantity for every line | Decrement stock + write `Issue` rows + notify — **one DB transaction** |
| `CancellationPending` | `Cancelled` | — | Restore stock + write `Adjustment` rows + notify |

Neither existed. `ApproveAsync` never looked at stock, and `IStockService.IssueAsync` had
**zero callers** anywhere in the solution. An approver could grant 500 units of an item with 3
in stock and the system would record it as approved. Cancelling an approved request likewise
never gave the stock back.

Separately, `Fulfilled` was in the status CHECK constraint and counted by
`ReportQueries`/`EligibilityQueries`, but **no transition ever set it** — one of the K3 values
invented by `StationerySchema.sql`. It also appeared as a My Requests filter that always
returned nothing.

## 2. The transaction problem, and how it is solved

`StockService.ApplyAsync` calls `SaveChangesAsync()` itself. Calling it from `ApproveAsync`
would give two commits — the stock could land while the status change failed, or vice versa.
Plan §3.6 and CLAUDE.md principle #6 both require one.

**Solution:** a second entry point that *stages* without saving.

```csharp
Task StageRequestMovementAsync(
    int itemId, int changeQuantity, StockTransactionType txType,
    int requestId, string reference, int actorEmployeeNumber);
```

It mutates the tracked `StationeryItem.QuantityAvailable` and adds the `StockTransaction`, then
returns. `ApproveAsync` / `ApproveCancellationAsync` already end in a single
`db.SaveChangesAsync()`, and **EF Core wraps one `SaveChanges` in one transaction**, so status +
history + notifications + stock + ledger commit or roll back together. No explicit
`BeginTransactionAsync` was needed, which also keeps the SQLite test provider happy.

`expectedRowVersion` is deliberately **absent** from the staged path. The approval already
gated on the *request's* row version; re-checking each item's would fail an approval because
somebody unrelated received stock for that item a second earlier.

## 3. Flow

### Approve (`POST /approvals/{id}/approve`)
1. Existing checks: approver, `Pending`, row version, one decision per line.
2. Decisions written to lines (`Decision`, `ApprovedQuantity` — unchanged from C2).
3. Header status derived (unchanged).
4. **New, when the outcome is not `Rejected`:**
   - **Pass one — verify.** Every line with `ApprovedQuantity > 0` is checked against
     `Item.QuantityAvailable`. Any shortfall → **422** listing every short item
     (`"Cannot approve — not enough stock: 'A4 Paper' has 5 in stock, 9 approved."`). Two
     passes so an under-stocked basket fails *before anything is staged* and the approver gets
     one complete message instead of a failure part-way through.
   - **Pass two — stage.** One `Issue` row per line, `ChangeQuantity = -ApprovedQuantity`
     (**never `Quantity`** — a reduced line issues what was granted), `RequestId` set,
     `Reference = "Request #N"`.
5. Status, history, notifications, one `SaveChangesAsync`.

A fully rejected request moves no stock.

### Decide a cancellation (`POST /approvals/{id}/cancel-approval`)
On **approval** of the cancellation, one `Adjustment` row per line with
`ChangeQuantity = +ApprovedQuantity`, `Reference = "Cancellation of Request #N"`, staged before
the same single save. `Adjustment` rather than `Receipt` because nothing physically arrived —
this reverses a movement.

Guarded on `ApprovedQuantity is > 0`, so a rejected line restores nothing, and a request that
somehow reaches `Cancelled` without ever having been approved restores nothing.

On **refusal**, stock is untouched.

## 4. Files

| Layer | File | Change |
|---|---|---|
| Core | `Entities/StockTransaction.cs` | `RequestId` (nullable) |
| Core | `Entities/Request.cs` | doc — no `Fulfilled` state |
| Application | `Interfaces/Inventory/IStockService.cs` | `StageRequestMovementAsync`; doc on `IssueAsync` |
| Application | `Interfaces/Requests/IRequestService.cs`, `Interfaces/Reports/IReportQueries.cs`, `DTOs/Reports/CostByItemReportDto.cs` | docs |
| Infrastructure | `Services/StockService.cs` | `StageRequestMovementAsync` |
| Infrastructure | `Services/RequestService.cs` | stock guard + issue on approve; restore on cancel; ctor takes `IStockService` |
| Infrastructure | `Data/Configurations/StockTransactionConfiguration.cs` | FK + index on `RequestId`, `SetNull` |
| Infrastructure | `Data/Configurations/RequestConfiguration.cs` | `Fulfilled` out of `CK_Requests_Status` |
| Infrastructure | `Queries/ReportQueries.cs`, `Queries/EligibilityQueries.cs` | `Fulfilled` out of the status sets |
| Infrastructure | `Data/DbSeeder.cs` | dead demo seeder no longer emits `Fulfilled` |
| Infrastructure | `Data/Migrations/20260904043931_AddStockIssueOnApproval.*` | **new** — see §5 |
| Frontend | `pages/requests/MyRequestsPage.jsx`, `components/RequestStatusBadge.jsx` | `Fulfilled` filter + badge removed |
| Frontend | `api/reports.js`, `pages/help/faqData.js` | docs / FAQ wording |
| Tests | `Tests/WebApi.IntegrationTests/ApprovalStockTests.cs` | **new**, 7 tests |

## 5. The migration

`Up()`: adds `StockTransactions.RequestId` (`int NULL`) + index + FK to `Requests(Id)`
`ON DELETE SET NULL`; drops and re-adds `CK_Requests_Status` without `'Fulfilled'`.

Between those, a hand-written data fix:

```sql
UPDATE [Requests] SET [Status] = 'Approved' WHERE [Status] = 'Fulfilled';
```

Required — the narrowed CHECK would be rejected by any surviving row. In practice it updates
zero rows, because nothing in the application ever set `Fulfilled`.

`SetNull` on the FK, not `Restrict`: the ledger is append-only truth and must outlive the one
deletion the system permits (a `Draft`, which by definition never moved stock).

Applied to `StationeryManagementSystem.Dev` on real SQL Server at API startup — clean.

## 6. Tests actually run

`dotnet test Project.slnx` — **174 passed** (54 unit + 120 integration). New in
`ApprovalStockTests`: decrement + one `Issue` row per line with the right `RequestId` and
actor · modified line issues `ApprovedQuantity` not `Quantity` · rejected line moves nothing ·
**under-stocked approval → 422 and commits nothing** (status still `Pending`, ledger empty) ·
approve-then-cancel restores with an `Adjustment` row · refused cancellation leaves stock alone ·
ledger sum equals the cached `QuantityAvailable`.

`npx vitest run --pool=threads` — **138 passed**. `npm run build` clean.

**Live run against real SQL Server** (item #2, "A3 Copy Paper"):

| Step | Stock | Ledger |
|---|---|---|
| before | 112 | — |
| approve request #108 (10 units) | **102** | `Issue −10, ref "Request #108", reqId 108, by 22` |
| approve its cancellation | **112** | `Adjustment +10, ref "Cancellation of Request #108", reqId 108` |

## 7. Reviewer follow-ups

1. **`Fulfilled` is gone.** If the team wants a real pick/pack step later, it needs its own
   transition, migration and UI — do not just put the value back in the CHECK.
2. **Partial approval and stock.** A `PartiallyApproved` request issues only the granted
   quantities. `TotalEstimatedCost` still holds the *requested* total, so reports sum requested
   rather than issued spend. That was already true before this change (see the C1–C6 handoff,
   follow-up 4) and is still a team decision.
3. **No reservation at submit.** Stock is checked and taken at *approval*, not held at
   submission, so two requests can both pass eligibility and the second can fail the stock
   guard. That matches Plan §3.6, which puts the stock guard on `Pending → Approved`.
4. **Concurrency.** The staged path does not compare item row versions (§2). Two approvers
   approving different requests for the same item at the same instant both read a fresh
   balance inside their own transaction; SQL Server serialises the writes and the second sees
   the first's decrement. A lost update would need both to read before either writes — possible
   in principle, and the negative-balance guard in `StageRequestMovementAsync` is the backstop.
