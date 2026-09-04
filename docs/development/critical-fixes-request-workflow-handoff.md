# Handoff — Critical fixes to the request workflow (audit findings C1–C6)

**Date:** 2026-09-04 · **Branch:** `khang` · **Migration in this change:** `20260904030457_AddDraftStatusAndLineDecisions` (announce it — one open migration PR at a time)

This follows `PROJECT_AUDIT.md`. It fixes the five findings rated **Critical** there, plus
**C6** (rated High) because fixing C5 made C6 reachable from the UI. Nothing else. Read the audit
first for the "why"; this document is the "what and how", written so another team member can
explain the code in review.

## 1. What was wrong, in one paragraph each

**C1 — no Draft.** Requests were created with `Status = "Pending"`. The approver's queue is
"everything Pending", so *Save as Draft* put the request in front of the approver immediately.
The UI worked around it by inspecting status history for a `Pending → Pending` row to guess
whether a request had been submitted — a guess the server never made.

**C2 — decisions thrown away.** The approver picks approve / reject / modify-qty per line. The
service counted those decisions to pick a header status and then never wrote them anywhere;
`RequestItem` had no column for them. A `PartiallyApproved` request could not say which lines
were approved, and a reduced quantity was silently lost.

**C3 — managers saw nothing extra.** `RequestQueries` read `ApplicationUser.RankLevel` to decide
Manager+ visibility. That column is never set by the real user-creation path and is 1 for
everyone. Every other file resolves rank from the *role* table.

**C4 — deletable after submission.** `DELETE /requests/{id}` accepted any `Pending` request.
Because Pending also meant "submitted" (C1), a requestor could erase a request already in the
approver's queue, cascade-deleting its audit history. Plan §3.6: "Never DELETE a request."

**C5 — cancellation dead end.** `GET /approvals/pending` returned `Pending` only, and no page
called `POST /approvals/{id}/cancel-approval`. Once a request reached `CancellationPending`, the
requestor could do nothing and the approver could not find it.

**C6 — refusing a cancellation always gave "Approved".** `ApproveCancellationAsync` loaded the
request without its `StatusHistory`, so the "what was it before?" lookup found nothing and fell
back to `Approved` — wrong for a `PartiallyApproved` request. Its validator was never injected.
And `ApprovalController` caught every exception itself, turning a validation failure into a
500 instead of the 400 every other controller returns.

## 2. The state machine as it now behaves

```
[*] ──create──► Draft ──submit──► Pending ──approve──► Approved
                 │                  │                    │ PartiallyApproved
                 └─delete (only     └─withdraw──► Withdrawn
                   deletable state)  └─reject───► Rejected
Approved / PartiallyApproved ──request cancellation──► CancellationPending
CancellationPending ──approver approves──► Cancelled
CancellationPending ──approver refuses──► whatever it was before (Approved / PartiallyApproved)
```

Transitions are still inline `if` checks in `RequestService` — the Plan's `RequestStateMachine`
class (T3.2) is *not* part of this change.

## 3. Flow: what happens on each action

### Create (`POST /requests`) → `RequestService.CreateAsync`
Validates lines, resolves the approver as the requestor's superior, checks each item's
`MinRankLevelToRequest` against the requestor's **role** rank, snapshots prices, and saves
`Status = "Draft"` with one history row (`null → Draft`). **No notification** — nobody but the
requestor knows a draft exists.

### Submit (`POST /requests/{id}/submit`) → `SubmitAsync`
Ownership + `RowVersion` check, requires `Draft`, sets `Pending`, history row `Draft → Pending`,
stages the `RequestSubmitted` notification for both parties, one `SaveChangesAsync`.
Submitting twice → 409.

### Approve (`POST /approvals/{id}/approve`) → `ApproveAsync`
Must be the listed approver; must be `Pending`; `RowVersion` must match; decision count must
equal line count. Then, **new:** each decision is matched to its line by `RequestItemId`
(unknown or duplicate id → 409) and written onto the line:

| decision | `RequestItem.Decision` | `RequestItem.ApprovedQuantity` |
|---|---|---|
| approved | `"approved"` | `Quantity` |
| rejected | `"rejected"` | `0` |
| modified | `"modified"` | `ModifiedQuantity` (validator guarantees > 0) |

Header status: every line `approved` → `Approved`; every line `rejected` → `Rejected`;
otherwise → `PartiallyApproved` (so a single reduced quantity is a partial approval — a reduced
quantity is by definition not a full one). `Quantity`, `LineTotal` and `TotalEstimatedCost` are
**never rewritten** — they are the snapshot of what was asked (CLAUDE.md principle #8).

### Delete (`DELETE /requests/{id}`) → `DeleteDraftAsync`
Ownership check; `Draft` only. Anything else → 400 with "Withdraw a submitted request instead."

### Approver queue (`GET /approvals/pending`) → `GetPendingApprovalsAsync`
Now `Status IN ('Pending', 'CancellationPending') AND ApproverEmployeeNumber = caller`. Drafts
are never in it.

### Decide a cancellation (`POST /approvals/{id}/cancel-approval`) → `ApproveCancellationAsync`
Now validates the command (reason ≤ 500 chars, etc.), loads `StatusHistory`, and on **refusal**
reverts to the `FromStatus` of the most recent transition *into* `CancellationPending` — i.e.
exactly the status the request held when the requestor asked. If no such history row exists
(cannot happen through the API) it throws 409 rather than guessing. On **approval** → `Cancelled`
+ notification. The new `CancellationDecisionModal` on the Approvals page calls it.

`ApprovalController` lost its three `try/catch` blocks; `ExceptionHandlingMiddleware` now maps
its exceptions like every other controller (CLAUDE.md #2).

## 4. Files

| Layer | File | Change |
|---|---|---|
| Core | `Entities/RequestItem.cs` | `Decision`, `ApprovedQuantity` |
| Core | `Entities/Request.cs` | default `Draft`; doc |
| Infrastructure | `Data/Configurations/RequestItemConfiguration.cs` | columns + `CK_RequestItems_Decision` |
| Infrastructure | `Data/Configurations/RequestConfiguration.cs` | `Draft` in `CK_Requests_Status`; default `Draft` |
| Infrastructure | `Data/Migrations/20260904030457_AddDraftStatusAndLineDecisions.*` | **new** — see §5 |
| Infrastructure | `Services/RequestService.cs` | Create / Submit / Approve / DeleteDraft |
| Infrastructure | `Queries/RequestQueries.cs` | `GetRankLevelAsync` (C3); queue filter (C5); DTO fields |
| Application | `Interfaces/Requests/IRequestService.cs`, `IRequestQueries.cs` | rename + docs |
| Application | `DTOs/Requests/RequestDto.cs`, `SubmitRequestCommand.cs` | `RequestItemDto` + 2 fields; docs |
| WebApi | `Controllers/RequestsController.cs` | `DeleteDraft` |
| WebApi | `Controllers/ApprovalController.cs` | try/catch removed (C6) |
| Frontend | `api/requests.js` | `deleteDraftRequest`; docs |
| Frontend | `pages/requests/MyRequestsPage.jsx` | Draft filter; actions keyed on status |
| Frontend | `pages/requests/components/RequestDetailModal.jsx` | status-keyed actions; Decision / Approved qty columns |
| Frontend | `pages/requests/components/RequestStatusBadge.jsx` | Draft badge |
| Frontend | `pages/requests/NewRequestPage.jsx` | draft success message |
| Frontend | `pages/requests/ApprovalsPage.jsx` | "Decide" on CancellationPending rows |
| Frontend | `pages/requests/components/CancellationDecisionModal.jsx` | **new** |
| Tooling | `.claude/launch.json` | `api` launch entry (SQLEXPRESS override as CLI arg) |
| Tests | `Tests/WebApi.IntegrationTests/RequestsTests.cs` | +9 tests, 3 renamed, helper `CreateAndSubmitAsync` |
| Tests | `MyRequestsPage.test.jsx`, `ApprovalsPage.test.jsx` | fixtures → Draft/Pending; +2 tests |

## 5. The migration

`Up()`:
1. Drops and re-adds `CK_Requests_Status` with `'Draft'` in the list.
2. `Requests.Status` default `'Pending'` → `'Draft'`.
3. Adds `RequestItems.Decision nvarchar(20) NULL`, `RequestItems.ApprovedQuantity int NULL`,
   and `CK_RequestItems_Decision` (`NULL` or one of the three values).
4. **Data fix (hand-written):** `UPDATE Requests SET Status='Draft' WHERE Status='Pending' AND
   NOT EXISTS (history row Pending→Pending)`. That history row was the old UI's "submitted"
   marker, so this converts exactly the rows the old UI would have shown as drafts. Rows that
   were genuinely submitted keep `Pending`.

`Down()` reverses all of it (Draft rows go back to Pending first, because the old CHECK
rejects `Draft`).

Applied to this machine's `StationeryManagementSystem.Dev` on SQLEXPRESS at API startup:
7 rows became `Draft`, 8 stayed `Pending`. This is the **first time any migration in this
repository has been executed against real SQL Server** (audit P3) — it went through cleanly.

## 6. Tests actually run

- `dotnet test Project.slnx` — **142 / 142** (53 unit + 89 integration).
- `npx vitest run --pool=threads` — **118 / 118** (20 files).
- Live browser run against the real API and SQL Server: the full lifecycle in §2, both
  accounts, every step checked in the UI, the API and the database. Details in the
  2026-09-04 entry of `AI_usage_report.md`.

New regression tests, each named for the finding it guards:
`Draft_IsInvisibleToApprover_UntilSubmitted` (C1) ·
`ApproveRequest_ModifiedLine_PersistsDecisionAndApprovedQuantity`,
`ApproveRequest_RejectedLine_PersistsZeroApprovedQuantity`,
`ApproveRequest_DecisionForForeignLine_Returns409` (C2) ·
`GetById_UnrelatedManager_SeesRequest_RankComesFromRole` (C3) ·
`DeleteRequest_AfterSubmit_Returns400AndKeepsRequest` (C4) ·
`PendingApprovals_IncludesCancellationPendingRequests` (C5) ·
`RefuseCancellation_PartiallyApprovedRequest_RevertsToPartiallyApproved`,
`ApproveCancellation_ReasonOver500Chars_Returns400` (C6 — the second one only passes once the
controller stops swallowing exceptions).

## 7. Known issues and reviewer follow-ups

1. ~~C6~~ — fixed in this change (see §1, §3).
2. **Stock still does not move on approval (C8).** `ApprovedQuantity` is now the number that a
   future `IStockService.IssueAsync` call must use — never `Quantity`.
3. **Budget still not enforced (C7).** The natural place is `SubmitAsync` (Plan §3.6 guard
   "Total ≤ role threshold").
4. `TotalEstimatedCost` is the *requested* total. If Reports should sum *approved* spend, that
   is a team decision (and a column or a computed projection), not made here.
5. The old UI heuristic is gone; if any other client relied on the `Pending → Pending` history
   marker, it must switch to `status === 'Draft'`.
6. Any seed script that inserts requests must now either insert `Draft` or insert the
   `Draft → Pending` history row — `DbSeeder.SeedDemoDataAsync` (dead code, never called)
   inserts statuses directly and would need updating if it is ever wired up.

## 8. How to run it locally

```bash
# API (from the repo root) — uses SQLEXPRESS without touching appsettings.Development.json
cd WebApi && dotnet run --no-launch-profile --environment ASPNETCORE_ENVIRONMENT=Development --urls http://localhost:5263 -- "--ConnectionStrings:DefaultConnection=Server=.\SQLEXPRESS;Database=StationeryManagementSystem.Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" "--Elasticsearch:Uri="
```

```bash
cd frontend && npm run dev
```

Or use the two entries in `.claude/launch.json` (`api`, `frontend`). Migrations apply on startup.
