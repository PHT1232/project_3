# PROJECT AUDIT — Stationery Management System

> **Current as of the RE-AUDIT on 2026-09-05**, branch `khang` @ `cf7c39f`,
> **plus the H1/H3 fixes applied later the same day** (see "High Priority Issues" below).
> Repository files are the evidence; the Plan (`__ai_agents/Stationery_Management_System_Project_Plan.md`)
> is the requirement. Written for an intern-level reader.
> Interactive version of the original audit: https://claude.ai/code/artifact/5729e52c-8155-4aba-b3e9-ea2f50d4c3d7

---

## Audit History

### Previous Audit — 2026-09-04 (`2663c52`)
Full repository scan. Found **9 confirmed logical errors (C1–C9)** — five Critical, three High,
one Medium — plus **7 potential issues (P1–P7)** needing a team decision, and 8 missing features.
Headline: the request approval workflow was structurally wrong in ways no test caught, because the
tests were written from the implementation rather than from Plan §3.6.

### Re-Audit — 2026-09-05 (`cf7c39f`)
All C1–C9 were re-verified **against the current code, not taken on trust**: each fix was read in
place, its logic checked against Plan §3.6, and its workflow traced end to end. Both suites were
re-run. The re-audit also covers two features that did not exist at the last audit (the **support
inbox** and **hierarchy-based request visibility**) and the **goods-arrival** rework of the
inventory flow.

Evidence run on this machine, 2026-09-05:

| Command | Result |
|---|---|
| `dotnet build Project.slnx` | 0 errors, 1 warning (`NU1903`, vulnerable SQLite test package) |
| `dotnet test Project.slnx` | **220 passed** (86 unit + 134 integration), 0 failed — was 183 before the H1/H3 and M1–M3 fixes |
| `npx vitest run --pool=threads` | **147 passed** across 24 files, 0 failed |

---

## Previously Reported Issues

| ID | Previous Severity | Current Status | Verification |
|---|---|---|---|
| **C1** Draft sent straight to approver | Critical | **Fixed and Verified** | `RequestService.cs:136` creates `Status="Draft"`; `:183` submit requires `Draft`; `CK_Requests_Status` now lists `Draft`; `GetPendingApprovalsAsync` filters `Pending`/`CancellationPending` only, so drafts never reach the queue. |
| **C2** Per-line decisions discarded | Critical | **Fixed and Verified** | `RequestService.cs:283-284` writes `line.Decision` and `line.ApprovedQuantity` per line, matched by `RequestItemId`; `:300-303` derives the header status from persisted line state. Requested `Quantity` is left untouched. |
| **C3** Managers cannot see all requests | Critical | **Fixed and Verified** (superseded by a better fix) | The stale `ApplicationUser.RankLevel` reads are gone. `RequestQueries` now takes `IHierarchyQueries` and scopes by reporting sub-tree (Plan §6 / TC-15) — stricter and more correct than the role-join originally recommended. `HierarchyQueries` reads rank from `AspNetRoles`. No stale-column reads remain anywhere (the one `target.RankLevel` in `UserManagementService:143` is a `UserDto` populated from the role join — checked). |
| **C4** Submitted request deletable | Critical | **Fixed and Verified** | `DeleteDraftAsync` returns false for any status other than `Draft` before reaching `db.Requests.Remove`. Ownership check precedes it. |
| **C5** Cancellation dead end | Critical | **Fixed and Verified** | `GetPendingApprovalsAsync` includes `CancellationPending`; `ApprovalsPage` renders a "Decide" action opening `CancellationDecisionModal`, which calls `/cancel-approval`. |
| **C6** Refusal always reverted to Approved | High | **Fixed and Verified** | `ApproveCancellationAsync` now `.Include(r => r.StatusHistory)` and reverts to the `FromStatus` of the most recent transition *into* `CancellationPending`, throwing 409 if no such row exists. `approveCancelValidator` is injected and called. |
| **C7** Spending limits never enforced | High | **⚠ Partially Fixed** | The **monthly** limit is enforced: `SubmitAsync` compares `TotalEstimatedCost` against `eligibility.RemainingThisMonth` and throws `BusinessRuleException` → 422. `CommittedStatuses` correctly excludes `Draft`, so a request does not count against itself. **But `MaxAmountPerRequest` is still never enforced anywhere** — it is stored, seeded and returned in `EligibilityDto`, and no code compares a request total to it. See current issue **M1**. |
| **C8** No stock movement / `Fulfilled` unreachable | High | **Fixed and Verified** | `ApproveAsync` pre-checks every line's availability, then stages `StageRequestMovementAsync` with **`ApprovedQuantity`** (not `Quantity`), in the same transaction as status + history + notifications. Cancellation restores stock, guarded on `ApprovedQuantity is > 0`. `Fulfilled` removed from the entity, the CHECK constraint, `ReportQueries`, `EligibilityQueries` and the UI filter — zero references remain repo-wide. |
| **C9** Blank category/supplier on lines | Medium | **Fixed and Verified** | `RequestQueries.cs:84-85` chains `.ThenInclude(item => item!.Category)` and `.ThenInclude(item => item!.Supplier)`. |
| **P1** Reports open to all vs Plan Manager+ | Potential | **Still Present** | `ReportsController` is `[Authorize]` only; `/reports` sits outside the manager group in `App.jsx`. Still contradicts Plan §4.2, T5.2 and TC-18. Needs a team ruling, not a silent fix. |
| **P2** Role-assignment escalation | Potential | **⚠ Partially Fixed** | `EnsureActorCanAssignRoleAsync` / `EnsureActorCanManageTarget` now block a **Business Manager** from creating or managing BM/MD accounts. But both guards test `currentUserService.RankLevel == BusinessManagerRankLevel` (3), so a **Manager (rank 2) is not covered and can still create or promote a Managing Director**. See current issue **H2**. |
| **P3** Migrations never executed by CI | Potential | **Still Present** | `CustomWebApplicationFactory:49` still uses `EnsureCreatedAsync()`. The 8 migrations and SQL Server-only defaults are never exercised by the suite. (They *have* now been applied manually to the SQLEXPRESS dev DB — but not by CI.) |
| **P4** N+1 in request listing | Potential | **Still Present — WORSENED** | Promoted to a confirmed issue; see **H1**. |
| **P5** No 401 response interceptor | Potential | **Still Present** | `api/client.js` has a request interceptor only (`interceptors.response` count = 0). |
| **P6** `Name` ≤ 15 / `Email` ≤ 25 chars | Potential | **Still Present** | `CreateUserRequestValidator:11-12` unchanged. 25 chars rejects most real corporate addresses. Conflicts with `StationerySchema.sql` (K2). |
| **P7** Dev connection string targets LocalDB | Potential | **Still Present** | `appsettings.Development.json:9` still `(localdb)\mssqllocaldb`; this machine has SQLEXPRESS only. Mitigated by the `api` entry in `.claude/launch.json`, which overrides it. |

**Score: 8 Fixed and Verified · 2 Partially Fixed (C7, P2) · 6 Still Present (all previously
"Potential") · 0 Cannot Verify.** No previously-fixed issue has regressed.

### Also resolved since the last audit (not previously tracked as numbered issues)

- **Help page** is now fully built (`pages/help/` with `FaqList`, `ContactCard`, `SystemInfoCard`) — was a placeholder.
- **Supplier order list** now exists as `SupplierOrdersModal` on the Inventory page — `getSupplierRequests()` was previously imported by nothing.
- **`ApprovalController` try/catch removed** — it now relies on `ExceptionHandlingMiddleware` like every other controller (0 `catch` blocks remain), so validation failures surface as 400/422 instead of 500.
- **Inventory receive flow reworked**: stock no longer rises when incoming stock is *recorded*. `POST /inventory/{itemId}/receive` was removed; a supplier order is raised `PendingArrival` and only a **Business Manager** confirming arrival posts the receipt, exactly once (409 on repeat).
- **`RequireManagingDirector` policy registered** — the support-inbox resolve endpoint previously threw 500 for everyone.

---

## Current Critical Issues

**None.** All nine previously-confirmed logical errors are closed, and the re-scan found no new
defect that corrupts data, bypasses authorisation, or blocks a core workflow.

---

## Current High Priority Issues

### ✅ H1 — Request listing was O(N) full-table scans per page — **FIXED AND VERIFIED 2026-09-05**
*Was: `GetVisibleAsync` resolved the scope once, then looped `GetByIdAsync` per row; each of those
**re-ran** `GetVisibleRequestorScopeAsync` (a rank query **and** a full `Users` adjacency load) plus
~4 more queries. A 100-row Dashboard page cost ~100 adjacency scans and ~400 round trips.*

**Fix.** `RequestQueries` now resolves the scope **once per call** and loads the whole page through
one private `LoadPageAsync`: one query for the rows (with all includes), one batched query for
every display name the page needs, then pure in-memory mapping. `GetByIdAsync` is now a
single-element call into the same path, so there is one place that decides visibility and one that
builds a `RequestDto`. All four list methods share it.

**Verified by measurement, not inspection.** EF command logging was switched on in dev and the
live API hit with `GET /requests?pageSize=100` (22 rows, 30 ids). SQL emitted per call:

| # | Query |
|---|---|
| 1 | rank lookup (`AspNetUserRoles ⋈ AspNetRoles`) — **once**, was once per row |
| 2 | `SELECT COUNT(*) FROM Requests` |
| 3 | `SELECT [r].[Id] … OFFSET/FETCH` |
| 4 | one `SELECT … WHERE [r].[Id] IN (30 ids)` with Items/Item/Category/Supplier/History joined |
| 5 | one `SELECT … FROM AspNetUsers WHERE [Id] IN (5 ids)` |

Zero per-row queries; the adjacency table is loaded at most once (and not at all for a Managing
Director, whose null scope short-circuits). **215 backend + 140 frontend tests pass.**

### H2 — A Manager can still create or promote a Managing Director *(confirmed; P2 half-fixed)*
**⚠ NOT FIXED — deliberately left pending a team ruling.** This descends from P2, a *Potential*
issue, and changing who may create a Managing Director alters the permission model rather than
correcting a defect. Raise it with the team before touching it.
`UserManagementService.EnsureActorCanAssignRoleAsync` and `EnsureActorCanManageTarget` only
engage when the actor's rank **equals 3** (Business Manager). A rank-2 Manager passes both
unchecked and can create a Managing Director account, or promote themselves. The BM restriction
that was added shows the team considers this class of escalation undesirable; the Manager case
looks like an oversight rather than a decision. **Confirm the intended rule before changing it.**

### ✅ H3 — `RequestStateMachine` did not exist — **FIXED AND VERIFIED 2026-09-05**
*Was: Plan T3.2 and CLAUDE.md #7 require one guarded `Transition()` as the only writer of
`Request.Status`. No such class existed; `RequestService` assigned `request.Status = …` directly in
5 places, and `Core/Entities/Request.cs` carried a doc comment asserting the opposite.*

**Fix.** `Application/Services/Requests/RequestStateMachine.cs` holds Plan §3.6's transition table
as data and owns the bookkeeping every transition shares — set status, append the
`RequestStatusHistory` row, rotate `RowVersion`. Illegal edges throw
`InvalidStateTransitionException` (derives from `ConflictException`, so it keeps mapping to **409**).
`RequestService` now has **0** direct `Status` writes; all five transition sites go through it. The
service keeps the guards the state machine cannot see — ownership, RowVersion, budget, stock — which
still run first. `Request.Status`'s doc comment now matches reality.

The table deliberately omits two statuses: `ReturnedForModification` (out of scope, K1) and
`Fulfilled` (removed by C8), so it agrees with `CK_Requests_Status`.

**Verified.** New `RequestStateMachineTests` types Plan §3.6 in as a matrix — 10 legal edges and
17 illegal ones — written from the *specification*, not the implementation, plus assertions that a
rejected transition leaves status, RowVersion and history untouched. Unit tests went **54 → 86**.
Live against SQL Server: approving a Draft as its real approver → **409** ("Cannot approve a request
in Draft status"), withdrawing a Draft → 409, submitting twice → 409, withdrawing a terminal
request → 409; the legal path Draft → Pending → Withdrawn wrote exactly the three expected audit
rows. **215 backend + 140 frontend tests pass; no regressions.**

---

## Current Medium Priority Issues

### ✅ M1 — `MaxAmountPerRequest` never enforced — **FIXED AND VERIFIED 2026-09-05**
*Was: only the monthly allowance gated submission; the per-request cap was stored, seeded and
shown on the Dashboard but never compared against anything — the unfinished half of C7.*

**Fix.** `SubmitAsync` now checks the per-request cap **before** the monthly one, so a
single-request breach names the right limit instead of blaming the month. A cap of `0` is treated
as "unset" (the column's default for roles created before the budget columns existed; enforcing it
literally would block every request). Both checks throw `BusinessRuleException` → 422.

**Verified.** Three new tests in `BudgetEnforcementTests` tighten the Engineer role's per-request
cap so it can be told apart from the monthly one: over-cap-but-within-month → 422 naming the
per-request limit and *not* the monthly wording; exactly at the cap → 200; cap of 0 → only the
monthly limit applies. Budget suite 7 → 10.

### ✅ M2 — `/support-inbox` unreachable from the UI — **FIXED AND VERIFIED 2026-09-05**
*Was: the route and page were fully built but had no navigation entry and no link anywhere, so
support messages sent from the Help page were invisible unless someone typed the URL.*

**Fix.** Added the nav entry (`Support Inbox`, `LifeBuoy` icon — already imported and unused,
suggesting this was started and abandoned) at **`minRankLevel: 2` — Manager and above only**, per
the team's instruction that it appear only in a manager account interface.

Also corrected a **frontend/backend mismatch found while doing it**: the route sat in the
Business-Manager+ (rank 3) group in `App.jsx`, while `SupportController`'s read endpoints are
`RequireManager` (rank 2). A Manager was allowed by the server but blocked by the SPA. The route
moved to the Manager+ group so nav floor, route guard and controller policy now all agree.
Resolving a message stays Managing-Director-only (`RequireManagingDirector`), which
`SupportInboxPage` already gates separately.

**Verified.** New `navigation.test.js` (7 cases) pins the rank floor and asserts the entry is
hidden from an Engineer and shown to Manager/BM/MD, plus a regression guard on the neighbouring
floors. Live: `GET /support/messages` → Engineer **403**, Manager **200**, Business Manager **200**;
in the browser the Manager's sidebar shows Support Inbox and the page loads with its empty state,
the Engineer's sidebar does not, and an Engineer typing `/support-inbox` is redirected to the
Dashboard.

### ✅ M3 — Integration tests never executed the migrations — **FIXED AND VERIFIED 2026-09-05**
*Was: `EnsureCreatedAsync()` builds the schema from the model, so none of the 8 migration files,
their CHECK constraints or their hand-written data fixes were ever run. A broken migration passed CI.*

**Fix.** The SQLite factory is unchanged — deliberately, because the migrations are
irreducibly SQL Server-flavoured (`NEWID()`, `GETUTCDATE()`, `ALTER TABLE … ADD CONSTRAINT … CHECK`,
bracketed T-SQL in the data fixes), so pointing `MigrateAsync` at SQLite would only fail. Instead a
new `MigrationTests` applies the real chain to a throwaway SQL Server database (`MigrateAsync`,
uniquely named, dropped afterwards) and asserts: nothing pending afterwards, **no pending model
changes** — which catches the stale-designer-snapshot mistake this branch hit twice — and that the
CHECK constraints the workflow depends on exist with the right vocabularies (`Draft` present,
`Fulfilled` absent, `PendingArrival`/`Received` present).

`RequiresSqlServerFactAttribute` skips cleanly when no LocalDB or SQLEXPRESS is reachable, so a
teammate without SQL Server does not get a red build. **On this machine both tests actually ran
and passed — they were not skipped.**

### M4 — Reports are open to every authenticated user *(P1, unchanged — needs a team ruling)*
**⚠ NOT FIXED — deliberately.** This is P1 renamed: a *Potential* issue where the code and the Plan
disagree in writing, not a defect. Whichever side wins, the other must be updated — a call for the
team, not a silent code change.
Row-scoping means an Engineer only sees their own spend, so nothing leaks. But Plan §4.2, T5.2
("Engineer → 403") and TC-18 all say Manager+. The code and the Plan disagree **in writing**;
whichever wins, the other must be updated.

---

## Current Low Priority Issues

- **L1** — `/new-request` is routed but absent from `navigation.js`; reachable only from the Catalogue's "Proceed" button or the My Requests header.
- **L2** — Dead code: `Application/Services/Service.cs` + `Application/Interfaces/IService.cs` (0 DI registrations) and `DbSeeder.SeedDemoDataAsync` (0 callers; its comment still claims a `Seed:DemoData` gate in `Program.cs` that does not exist).
- **L3** — No 401 response interceptor (P5): an expired 8-hour JWT surfaces as raw errors rather than a redirect to login.
- **L4** — `Name` ≤ 15 / `Email` ≤ 25 character limits (P6) will reject realistic names and addresses in a demo.
- **L5** — Dev connection string targets LocalDB (P7); use the `api` entry in `.claude/launch.json`, which overrides to SQLEXPRESS.
- **L6** — Repeated failed submissions from New Request create one orphaned Draft each. Mitigated: the error message names the draft id and tells the user to continue from My Requests, and Drafts do not consume budget.
- **L7** — `CLAUDE.md` §1 is still badly stale — it says "Everything after M2 is still unbuilt", which is wrong by five milestones. It is the first file every teammate and AI agent reads.
- **L8** — `bin/` and `obj/` are still tracked repo-wide; `git status` shows dozens of modified `.dll` files before anyone edits code.

---

## Potential Issues Requiring Confirmation

- **PC1 — Supplier-order arrival has no concurrency token.** Duplicate confirmation is blocked by a status check inside the transaction, which covers sequential double-clicks (tested). Two confirmations racing in separate transactions could in principle both read `PendingArrival`; `SupplierRequest` has no `RowVersion`. Needs a decision on whether to close the race.
- **PC2 — Partial deliveries are not modelled.** A supplier order is all-or-nothing. Nothing in the Plan or the task specifies per-line receipt; confirm whether it is wanted.
- **PC3 — Plan/schema now trail the code in three places.** Plan §4.2 still lists the removed `POST /inventory/{itemId}/receive`; neither the Plan (§3.3 table 11) nor `StationerySchema.sql` describes the supplier-order lifecycle; `supplier-request-cart-implementation-handoff.md` still says stock moves "later via `receiveGoods()`". Documentation debt, not code defects.
- **PC4 — Two concurrency strategies coexist.** `Request` uses EF's `IsConcurrencyToken()`; `StationeryItem`/`Supplier` use app-managed Guid compare-then-set. Both work; the inconsistency is a trap for the next person.
- **PC5 — `Comment` is optional on rejection.** Plan §3.6 lists "Comment required" as the guard on `Pending → Rejected`. `ApproveRequestCommandValidator` only caps its length. Confirm whether the Plan's guard should be enforced.
- **PC6 — AI catalogue cap.** `RequestAssistantService.CatalogueLoadLimit = 500`. If the catalogue ever exceeds 500 items the assistant silently stops seeing the rest.

---

## Current Project Status — **PASS WITH ISSUES / stable enough to continue**

**Update 2026-09-05 (post-fix).** Of the three High issues, **H1 and H3 are fixed and verified**;
**H2 is deliberately still open** because it needs a team ruling, not a code change. Request
listing is now a fixed 4 queries per page (measured), and `RequestStateMachine` is the sole writer
of `Request.Status`, backed by a specification-derived transition matrix.

**Of the four Medium issues, M1, M2 and M3 are fixed and verified; M4 is deliberately still open**
(it is P1 renamed — the Plan and the code disagree in writing and the team must pick). The
per-request spending cap is now enforced, Support Inbox is reachable and Manager-only with the
route guard corrected to match the controller policy, and the migration chain is executed against
real SQL Server by CI-skippable tests.

Tests: backend **183 → 220**, frontend **140 → 147**. Everything below still holds.

**What is now working correctly.** The full request lifecycle behaves as Plan §3.6 specifies:
create → `Draft` (invisible to the approver, the only deletable state) → submit (budget-gated,
422 when over) → approve/reject with **persisted per-line decisions** → stock issued for the
approved quantity in one transaction → cancellation restores it. Manager visibility follows the
reporting sub-tree. The cancellation round-trip completes in the UI and reverts to the correct
prior status. Inventory no longer rises before goods physically arrive — only a Business Manager
confirming a supplier order posts a receipt, exactly once. Authentication, user management,
catalogue, suppliers, notifications, the five reports, the support inbox, the Help page and the
AI Request Assistant (grounded, rate-limited, offline fallback, key never committed) are all
implemented and covered. **183 backend + 140 frontend tests pass**, including regression tests
named for each closed finding.

**What remains incomplete.** Two items, both awaiting a decision rather than code: the
Manager-level half of the role-escalation guard (**H2**) and the reports-policy contradiction
(**M4**). Everything else outstanding is Low priority or documentation.

**What still needs attention first.** **H2 and M4 both need a team ruling** — may a Manager create
or promote a Managing Director, and are reports Manager+ or open to all? Neither should be changed
on one person's judgement. After that, the Low items are cleanup: dead code (L2), the 401
interceptor (L3), the `/new-request` nav entry (L1), and the documentation reconciliations in
PC3 — the Plan and `StationerySchema.sql` now trail the code in several places, and CLAUDE.md §1
is actively misleading (L7).

**Is it stable enough for continued development and testing? Yes.** Nothing is Critical, no
previous fix regressed, both suites are green, and every core workflow completes end to end
against real SQL Server. The remaining work is hardening, performance and documentation — not
correctness of the primary flows.

---

## Appendix — original findings (2026-09-04), retained for reference

The nine confirmed errors as first reported. All are closed; C7 only partially (see M1).

| ID | Problem as first reported |
|---|---|
| C1 | "Save as Draft" sent the request to the approver — no `Draft` status existed; the UI faked one by pattern-matching status history. |
| C2 | `ApproveAsync` counted per-line decisions then discarded them; `RequestItem` had no column to store them. |
| C3 | `RequestQueries` gated Manager+ visibility on `ApplicationUser.RankLevel`, a column nothing populates (always 1). |
| C4 | `DELETE /requests/{id}` removed any `Pending` request, cascade-deleting the audit history of an already-submitted request. |
| C5 | `CancellationPending` was unreachable: the approver queue returned `Pending` only and no page called `/cancel-approval`. |
| C6 | Refusing a cancellation always reverted to `Approved` (missing `.Include(StatusHistory)`); its validator was never injected. |
| C7 | Role spending limits were displayed but never enforced on submission. |
| C8 | Approval moved no stock (`IssueAsync` had zero callers), nothing checked availability, and `Fulfilled` was unreachable yet counted. |
| C9 | Request lines always showed null category/supplier (missing `ThenInclude`s). |

Missing features listed in the original audit are now all delivered except the `RequestStateMachine`
(H3) and the two documentation items in PC3.
