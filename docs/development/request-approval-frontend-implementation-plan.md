# Request & Approval Frontend Implementation Plan

> Planned 2026-08-30 against `main` at `d44cc55` (after pulling PR #5 "thanh" — Request/Approval
> Core entities, DTOs, validators, `RequestService`/`RequestQueries` implementations, EF configs,
> migration `AddRequestEntities`, and one controller, `ApprovalsController`).
>
> Scope: `New Request`, `My Requests`, `Approvals` — the three requestor/approver-facing pages
> currently `PagePlaceholder` stubs owned by "M4 (Plan §6.1)".

## 0. Current state — verified against the actual code, not assumed

The backend is further along than its own handoff doc (`request-entity-dto-implementation-handoff.md`,
still stale) claims. Confirmed present and wired into `Program.cs`:

- **Core entities**: `Request`, `RequestItem`, `RequestStatusHistory` — header/line/audit-trail
  split, `Guid RowVersion` concurrency (app-managed, matching the M2 pattern), cost snapshotting.
- **Application layer**: `CreateRequestCommand`, `SubmitRequestCommand`, `ApproveRequestCommand`,
  `WithdrawRequestCommand`, `RequestCancellationCommand`, `ApproveCancellationCommand`,
  `RequestDto`/`RequestItemDto`/`RequestStatusHistoryDto`, matching FluentValidation validators,
  `IRequestService`, `IRequestQueries`.
- **Infrastructure**: `RequestService` (398 lines — full lifecycle, ownership checks, rank
  eligibility on create, concurrency checks, all transitions), `RequestQueries` (303 lines),
  `RequestConfiguration`/`RequestItemConfiguration`/`RequestStatusHistoryConfiguration`, migration
  `20260830072208_AddRequestEntities`.
- **WebApi**: **one** controller, `ApprovalsController` (`api/v1/approvals`), exposing exactly 3
  of the ~9 service/query methods: `GET /pending`, `POST /{id}/approve`, `POST /{id}/cancel-approval`.

### The blocking gap

**There is no `RequestsController`.** `CreateAsync`, `SubmitAsync`, `WithdrawAsync`,
`RequestCancellationAsync`, `DeletePendingAsync`, `GetVisibleAsync`, `GetByIdAsync`,
`GetByRequestorAsync`, and `GetStatusSummaryForDashboardAsync` are all implemented in
`RequestService`/`RequestQueries` but have no HTTP endpoint. This blocks **New Request** and **My
Requests** entirely — only **Approvals** can be built against what exists today.

This plan includes the missing controller as delivery step 1, since two of the three requested
pages cannot function without it. **Flagging before touching it**: `RequestService.cs` is
`git blame`-fresh from teammate "thanh"'s just-merged PR. Adding a controller that calls their
service is additive (not editing their files), but confirm they're not already mid-PR on this
before I start, to avoid duplicate work — see the question at the end of this doc.

### Other things also missing that this plan does not build

- **Stock does not move on approval.** `RequestService.ApproveAsync` never calls
  `IStockService.IssueAsync` — confirmed by reading the method body, not just the doc comment
  that says "M4". The frontend must not imply approval deducts inventory (no "stock updated"
  toast, no inventory refetch on approve).
- **Dashboard's request-summary widget** (`GetStatusSummaryForDashboardAsync`) has no consumer —
  `Dashboard.jsx` is still `PagePlaceholder`, "owner not assigned in the Plan". Out of scope here;
  the query exists and can be wired later without backend changes.
- **No tests exist** for the Request/Approval domain at all (`Tests/**/*Request*` only matches
  `SupplierRequestsTests.cs`, a different domain). Step 6 below adds them.

---

## 1. Backend: the missing `RequestsController`

`WebApi/Controllers/RequestsController.cs`, `[Route("api/v1/requests")]`, `[Authorize]` (any
authenticated user — ownership is enforced inside `RequestQueries`/`RequestService`, not by a
rank policy, per `IRequestQueries`' own doc comment: "Requestors see only their own... Approvers
see... Managers see all").

| Method | Route | Calls | Notes |
|---|---|---|---|
| GET | `/requests` | `IRequestQueries.GetVisibleAsync` | `?page=&pageSize=&status=` |
| GET | `/requests/{id}` | `IRequestQueries.GetByIdAsync` | 404 if not visible to caller (not 403 — ownership-aware, CLAUDE.md principle #9) |
| POST | `/requests` | `IRequestService.CreateAsync` | Body: `CreateRequestCommand`. 201. |
| POST | `/requests/{id}/submit` | `IRequestService.SubmitAsync` | Body: `{ rowVersion }`. |
| POST | `/requests/{id}/withdraw` | `IRequestService.WithdrawAsync` | Body: `{ rowVersion }`. |
| POST | `/requests/{id}/request-cancellation` | `IRequestService.RequestCancellationAsync` | Body: `{ rowVersion, reason? }`. |
| DELETE | `/requests/{id}` | `IRequestService.DeletePendingAsync` | Only for not-yet-submitted Pending requests. |

Exception mapping follows the existing `ExceptionHandlingMiddleware` convention already used
everywhere else (`NotFoundException` → 404, `ConflictException` → 409, `ValidationException` →
400) — **not** `ApprovalsController`'s local `try/catch` + `Problem(...)` pattern, which
duplicates what the middleware already does globally. Flagging this as an inconsistency worth
raising with "thanh", not silently fixing their file.

`GET /requests/{id}/subordinates`-style extra endpoints are not needed — visibility is already
parameterized by caller in the query layer.

---

## 2. Frontend architecture

Mirrors the M2 pattern exactly (established convention, five other pages already follow it):

- `frontend/src/api/requests.js` — thin client wrapper, one function per endpoint above plus
  `approveRequest`/`getPendingApprovals`/`approveCancellation` against the existing
  `ApprovalsController` routes.
- `frontend/src/pages/requests/NewRequestPage.jsx` (replaces `pages/NewRequest.jsx`)
- `frontend/src/pages/requests/MyRequestsPage.jsx` (replaces `pages/MyRequests.jsx`)
- `frontend/src/pages/requests/ApprovalsPage.jsx` (replaces `pages/Approvals.jsx`)
- Shared: `frontend/src/pages/requests/components/{RequestStatusBadge,RequestDetailModal}.jsx`

Reuse, don't reinvent:
- `useAsync`, `StateBlock` (loading/error/empty), `Card`, `Button`, `Modal`, `Badge`,
  `PageHeader` — same primitives every other page uses.
- The catalogue item picker in New Request reuses `getItems()` from `api/catalogue.js`
  (already role-filtered server-side) rather than a new endpoint.
- The line-items table UX (add row, quantity input, running subtotal) is structurally the same
  as `InventoryPage`'s cart pattern (`pages/inventory/components/SupplierRequestModal.jsx`) —
  worth skimming for the interaction pattern, not copying wholesale (different data shape).

---

## 3. `api/requests.js` contract

```js
// Requestor
export async function getMyRequests({ page = 1, pageSize = 20, status } = {}) { ... }   // GET /requests?... (server filters to caller's own when not Manager+)
export async function getRequest(requestId) { ... }                                      // GET /requests/{id}
export async function createRequest({ items, requiredByDate }) { ... }                   // POST /requests
export async function submitRequest(requestId, rowVersion) { ... }                       // POST /requests/{id}/submit
export async function withdrawRequest(requestId, rowVersion) { ... }                     // POST /requests/{id}/withdraw
export async function requestCancellation(requestId, rowVersion, reason) { ... }         // POST /requests/{id}/request-cancellation
export async function deletePendingRequest(requestId) { ... }                            // DELETE /requests/{id}

// Approver
export async function getPendingApprovals({ page = 1, pageSize = 20 } = {}) { ... }       // GET /approvals/pending
export async function approveRequest(requestId, rowVersion, lineDecisions, comment) { ... } // POST /approvals/{id}/approve
export async function approveCancellation(requestId, rowVersion, approved, reason) { ... }  // POST /approvals/{id}/cancel-approval
```

`RequestDto` (what every read returns) already carries everything the UI needs: requestor/approver
name, status, total cost, required-by date, decision comment, timestamps, `rowVersion`, full
`items[]`, full `statusHistory[]`. No separate "detail" vs "list" DTO — list rows just render a
subset of the same shape.

---

## 4. New Request page

Wireframe: `docs/Wireframe/Request.png`. Two fields on the wireframe have **no backing field in
`CreateRequestCommand`** — do not build them without a Plan update:

- **"Department"** — K5 in `CLAUDE.md` §6, already flagged as NOT SPECIFIED for Catalogue/New
  Request/Approvals filters. Omit the field entirely (same treatment `CatalogueFilters.jsx`
  already gives "Available to Me").
- **"Justification / Notes"** — no `Reason`/`Justification` field on `CreateRequestCommand` at
  all. Omit; do not invent a field the backend can't accept.
- **AI Request Assistant panel** — M5, explicitly out of scope (`page-map.md` §16, "P0 — graded"
  but a separate milestone). Render nothing here rather than a fake panel.

What to build, mapped to real fields:
- Required By Date (optional, `DateTime?`)
- Line items table: item picker (search/select from `getItems()`, respecting the role filter the
  API already applies), quantity input, unit cost + subtotal **read from the selected item's
  current `unitCost`** for display only — the server snapshots the real `UnitCostSnapshot` at
  creation time, so client-side totals are an estimate, not authoritative. Label it "Est. Total"
  to match the wireframe's own wording, not "Total".
- **"Save Draft" → `createRequest()` only** (leaves status `Pending`, unsubmitted).
- **"Submit Request" → `createRequest()` then immediately `submitRequest()`** with the returned
  `rowVersion`, in one user action.
- Validation mirrors `CreateRequestCommandValidator`: items non-empty, no duplicate `itemId`,
  each quantity `> 0` and `< 10000`.
- Rank-eligibility errors (`item.MinRankLevelToRequest > requestor.RankLevel`) come back as a 409
  `ConflictException` from `CreateAsync` — since `getItems()` already filters the picker to
  eligible items, this should be unreachable in normal use; still handle it as a generic error
  surface, not a silent failure, in case of a race (item's rank requirement changes between
  picker load and submit).

## 5. My Requests page

**No wireframe exists for this page** (only Dashboard/Catalogue/Request/Approvals/Inventory are
wireframed, per `CLAUDE.md` §7). Design it consistently with the table pattern already
established in `UserManagementPage`/`ItemManagement`/`SupplierManagement`:

- Paged table: status badge, required-by date, line-item count, est. total, submitted date.
- Status filter (dropdown over the `Status` enum values).
- Row click → `RequestDetailModal` (shared with Approvals, read-only mode) showing full line
  items + status history timeline.
- Row actions, conditional on status:
  - `Pending` (unsubmitted) → Submit, Delete (calls `deletePendingRequest`).
  - `Pending` (submitted, awaiting approval) → Withdraw.
  - `Approved`/`PartiallyApproved` → Request Cancellation (opens a reason prompt, calls
    `requestCancellation`).
  - Everything else (`Rejected`, `Withdrawn`, `CancellationPending`, `Cancelled`) → read-only.

Note the wire-level ambiguity CLAUDE.md doesn't resolve either: `RequestDto.Status` has no
separate "submitted" flag — re-reading `RequestService.SubmitAsync`, "submit" transitions
`Pending → Pending` (writes a status-history row marked "submitted" but the header `Status`
string doesn't change). The UI must distinguish "not yet submitted" from "submitted, awaiting
approval" by checking whether `StatusHistory` contains a `ToStatus == "Pending"` entry with a
`FromStatus` of `"Pending"` (the submit event), not by `Status` alone. Confirm this reading
against `RequestService.cs:127-166` before relying on it — flagged here rather than guessed
silently, since it's exactly the kind of state-encoding detail that's easy to get backwards.

## 6. Approvals page

Wireframe: `docs/Wireframe/Approvals.png`. Same K5 "Department" gap as New Request — omit the
department filter/column, keep everything else:

- Table: requester, date submitted, item-count summary, est. cost, status badge, "Review" action.
- Date-range filter — client-side over the loaded page is fine at this scale (no
  server-side date filter exists on `GetPendingApprovalsAsync`; don't invent one, filter what's
  already fetched).
- "Review" opens `RequestDetailModal` in **decision mode**: per-line radio (Approved / Rejected /
  Modified, with a quantity input that only enables for "Modified" — mirrors
  `ApproveRequestCommandValidator`'s rule that `ModifiedQuantity` is required only for
  `'modified'`), an overall comment field (max 1000 chars), Approve button.
- Cancellation requests (`CancellationPending` status) need their own review action —
  `POST /approvals/{id}/cancel-approval` with `approved: true/false` + optional reason. Wireframe
  doesn't show this state; design a simple two-button (Approve/Deny) variant of the same modal
  rather than the full line-decision form, since there's nothing to decide per-line here.

---

## 7. Shared components

- **`RequestStatusBadge.jsx`** — maps `Pending`/`Approved`/`PartiallyApproved`/`Rejected`/
  `Withdrawn`/`CancellationPending`/`Cancelled`/`Fulfilled` to `Badge` tones, following the
  existing `StockStatusBadge.jsx` pattern (small, presentational, no logic).
- **`RequestDetailModal.jsx`** — the one component used by both My Requests (read-only) and
  Approvals (decision mode), keyed by a `mode` prop (`'view' | 'decide'`), same pattern as
  `StockActionModal`'s `mode` prop in M2. Renders: header fields, line-items table, status-history
  timeline (`StatusHistoryDto[]`, newest first), and — only in `decide` mode — the per-line
  decision form.

---

## 8. Tests

Backend (none exist today):
- `Tests/Application.UnitTests/Requests/RequestServiceTests.cs` — rank-eligibility rejection on
  create, ownership checks (wrong approver → 404 not 403, per principle #9), concurrency 409 on
  stale `RowVersion`, status-guard rejections (e.g., submit a non-Pending request → 409),
  approve-outcome computation (all-approved/all-rejected/mixed → Approved/Rejected/PartiallyApproved).
- `Tests/WebApi.IntegrationTests/RequestsTests.cs` (new controller) + `ApprovalsTests.cs` — full
  lifecycle: create → submit → approve, create → withdraw, create → submit → approve → request
  cancellation → approve cancellation, against real SQLite in-memory (matching the
  `CustomWebApplicationFactory` pattern every other integration test file already uses).

Frontend:
- `NewRequestPage.test.jsx` — loading/error/empty item picker, validation (empty items, duplicate
  item, qty out of range), save-draft vs submit distinction.
- `MyRequestsPage.test.jsx` — status filter, submit/withdraw/delete actions per status, 409
  surfacing on stale-RowVersion retry.
- `ApprovalsPage.test.jsx` — pending list, per-line decision form validation (modified requires
  quantity), approve happy path, cancellation approve/deny variant.

---

## 9. Delivery steps (commits, following the M1/M2 convention — one step per commit)

1. `RequestsController.cs` + DI (nothing new to register — `IRequestService`/`IRequestQueries`
   already wired in `Program.cs`).
2. `api/requests.js`.
3. `RequestStatusBadge.jsx`, `RequestDetailModal.jsx` (shared components first, since both pages need them).
4. `NewRequestPage.jsx` + wire into `App.jsx`/`navigation.js` (replace `pages/NewRequest.jsx`).
5. `MyRequestsPage.jsx` (replace `pages/MyRequests.jsx`).
6. `ApprovalsPage.jsx` (replace `pages/Approvals.jsx`).
7. Backend tests (unit + integration).
8. Frontend tests.
9. `AI_usage_report.md` entry + handoff doc, following the established format.

---

## 10. Open questions — flagging, not deciding unilaterally

1. **Is "thanh" already building `RequestsController`?** This plan's step 1 fills a gap in their
   just-merged PR. Worth a quick check before starting, to avoid two people writing the same file.
2. **Submitted-vs-Pending state encoding** (§5 above) — my reading of `RequestService.cs` says
   "submit" is a status-history-only event, not a `Status` change. Worth a second pair of eyes
   before the frontend bakes in that assumption.
3. **`ApprovalsController`'s local exception handling** vs. the global middleware everyone else
   uses — do we ask "thanh" to align it, or leave it (functionally equivalent, just inconsistent
   style)?
4. **Department/Justification fields on the wireframes** — same K5 status as Catalogue's
   "Available to Me"/"Notify Me". Confirmed still unspecified; not resolving it here, just not
   inventing it either.
