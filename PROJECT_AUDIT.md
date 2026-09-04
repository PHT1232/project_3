# PROJECT AUDIT — Stationery Management System

> Deep scan of the whole repository on **2026-09-04**, branch `khang` @ `2663c52`.
> Written for an intern-level reader. Repository files are the evidence; the Plan
> (`__ai_agents/Stationery_Management_System_Project_Plan.md`) is the requirement.
> Interactive version: https://claude.ai/code/artifact/5729e52c-8155-4aba-b3e9-ea2f50d4c3d7

## 1. Overall status — **Needs Attention**

What was run on this machine:

| Command | Result |
|---|---|
| `dotnet build Project.slnx` | 0 errors, 2 warnings (`NU1903` on a SQLite test package) |
| `dotnet test Project.slnx` | 133 passed (53 unit + 80 integration), 0 failed |
| `npx vitest run --pool=threads` | 116 passed across 20 files, 0 failed |

Far more is built than `CLAUDE.md` §1 says — that block is a week stale. Requests, approvals,
notifications, reports, supplier orders, role budgets and the AI assistant have all shipped.
The reason for "Needs Attention" is that the **request approval workflow** — the core flow —
behaves incorrectly in ways no test catches. The tests pass because they test what the code
does, not what the Plan says it must do.

## 2. How the project works

- **Frontend** (`frontend/`) — React 18 + Vite + Tailwind. Draws every screen; holds no rules.
- **Backend** (`Core` → `Application` → `Infrastructure` / `WebApi`) — ASP.NET Core 10, Clean
  Architecture. Dependencies point inward; `Core` depends on nothing.
- **Database** — SQL Server via EF Core; 7 migrations in `Infrastructure/Data/Migrations/`.

`RequestService`, `StockService`, `NotificationService` and `SupplierRequestService` live in
`Infrastructure/Services/` **on purpose** — they need `DataContext` for multi-table commits, and
`Application` may not reference EF Core. Do not move them.

## 3. Implemented (confirmed by code + tests)

Auth (Identity + JWT, login by employee number or email, lockout, immediate deactivation) ·
User management CRUD · Catalogue (rank-filtered) + item management · Inventory with append-only
stock ledger · Suppliers + supplier orders · Notifications (dual-party, 30 s polling) · Five
reports (three mandated + two extra) with CSV export · AI Request Assistant with offline
fallback · RFC 7807 errors · Serilog · Swagger · loading/empty/error states everywhere · 404
page · no mock data left.

## 4. Partially implemented

| Feature | Works | Missing |
|---|---|---|
| Request lifecycle | create / submit / withdraw / approve / reject / request-cancel / history | no `Draft`, no state-machine class, no stock movement, `Fulfilled` unreachable, cancellation dead-ends |
| Partial approval | UI collects per-line decisions; API accepts them | decisions are counted then **discarded** |
| Spending eligibility | limits seeded on `AspNetRoles`; Dashboard shows remaining budget | never enforced on create/submit |
| Supplier orders | created from inventory cart; `GET` endpoint tested | no list page; no status column |
| Manager-wide visibility | code branches exist | read the wrong column → never trigger |
| Help page | route + nav | placeholder |

## 5. Missing features

| Feature | Expected (Plan) | Current | Priority |
|---|---|---|---|
| Stock movement on approval | §3.6: `Pending→Approved` decrements stock + writes `Issue` rows, guarded by stock ≥ qty | `IStockService.IssueAsync` has zero callers | HIGH |
| `Draft` status | §3.6 opens at `Draft`; `Draft→Pending` on submit | requests are born `Pending` | HIGH |
| `RequestStateMachine` | T3.2 / CLAUDE.md #7: one guarded `Transition()` | inline `if`s in six methods | HIGH |
| Budget enforcement | §3.6 submit guard "Total ≤ role threshold" | read + displayed only | HIGH |
| Cancellation decision UI | approver approves/refuses cancellation | endpoint exists, no page calls it, list endpoint can't surface these | HIGH |
| Stock restore on cancel | §3.6 `CancellationPending→Cancelled` restores stock | not implemented | MEDIUM |
| Supplier order list page | see orders you placed | `getSupplierRequests()` imported by nothing | MEDIUM |
| Help page content | T6.1 | placeholder | LOW |

Deliberately absent, correctly: `ReturnedForModification` (K1) and everything on the `[CUT]` list.

## 6. Confirmed logical errors

> **Status 2026-09-04 (later the same day):** **C1–C6 are FIXED** — see
> `docs/development/critical-fixes-request-workflow-handoff.md` and the matching
> `AI_usage_report.md` entries. Verified by 142 backend + 118 frontend tests and live browser runs
> against SQL Server. **C7, C8, C9 remain open**, as do all P-items. The rows below describe the
> state *before* the fixes.

| ID | Problem | Evidence | Impact | Recommended fix |
|---|---|---|---|---|
| **C1** | "Save as Draft" sends the request to the approver | `createRequest()` → `Status="Pending"`; `GetPendingApprovalsAsync` filters `Status=="Pending"`; `SubmitAsync` is `Pending→Pending`; UI fakes drafts via `isRequestSubmitted()` history pattern-match | drafts appear in approver queue and can be approved | add real `Draft` status (migration); create → `Draft`; submit → `Pending` |
| **C2** | Per-line approval decisions discarded | `ApproveAsync` counts `LineDecisions`, never writes them; `ModifiedQuantity` dropped; `modifiedCount` unused; `RequestItem` has no decision column | a `PartiallyApproved` request cannot say which lines were approved; all-modified → `PartiallyApproved` with nothing rejected | add `Decision` + `ApprovedQuantity` to `RequestItems`; persist; fix status derivation |
| **C3** | Managers cannot see all requests | `RequestQueries.cs:36,102,242,294` read `ApplicationUser.RankLevel`, which `IdentityUserStore` never sets (always 1). `ReportQueries`/`RequestService` join `AspNetRoles.RankLevel` instead, with comments saying why | manager-wide visibility never activates | replace the 4 reads with the role join |
| **C4** | A submitted request can be permanently deleted | `DeletePendingAsync` removes any `Pending` request; cascade takes items + history. Plan §3.6: "Never DELETE a request". UI hides the button; API does not | audit trail destroyed | delete only `Draft` |
| **C5** | Cancellation request is a dead end | `GET /approvals/pending` returns `Pending` only; `ApprovalsPage.jsx` never calls `/cancel-approval` (its own comment admits it) | `CancellationPending` is stuck forever | include `CancellationPending` in the approver list; add decision UI |
| **C6** | Denying a cancellation always reverts to `Approved` | `ApproveCancellationAsync` loads without `.Include(StatusHistory)` so the revert lookup falls to `?? "Approved"`; `ApproveCancellationCommandValidator` never injected | `PartiallyApproved` silently becomes `Approved` | include history; inject validator |
| **C7** | Spending limits shown, never enforced | `IEligibilityQueries.GetForEmployeeAsync` has one caller: `GET /users/me/eligibility`; `CreateAsync` never checks | Engineer with 500 limit can raise 50 000 | check on submit per §3.6 |
| **C8** | Nothing checks stock; `Fulfilled` unreachable | no availability check on create/approve; `IssueAsync` uncalled; only the dead demo seeder writes `Fulfilled`, yet `ReportQueries`/`EligibilityQueries` count it | over-requesting approved silently; filter option for a status that never occurs | stock guard + `IssueAsync` on approval |
| **C9** | Category/supplier names blank on request lines | `GetByIdAsync` includes `Items.Item` but not `Category`/`Supplier`; reads `i.Item?.Category?.Name` under `AsNoTracking` | always `null` → "General"/"Preferred Supplier" placeholders | add the two `ThenInclude`s |

## 7. Potential logical issues (need confirmation — not bugs yet)

- **P1** Reports are `[Authorize]`-only, but Plan §4.2, T5.2 and TC-18 say Manager+. Deliberate, documented deviation — team must pick one and update the other.
- **P2** A Manager can create/promote a Managing Director (`CreateUserRequestValidator` only requires `Role` non-empty). No doc specifies a cap.
- **P3** Integration tests use `EnsureCreatedAsync()` on SQLite — the 7 migrations and SQL Server-only defaults (`NEWID()`, `GETUTCDATE()`) are never executed by CI.
- **P4** N+1: `GetVisibleAsync` calls `GetByIdAsync` per row (~4 queries each); Dashboard asks for 100 rows.
- **P5** No 401 response interceptor in `api/client.js`; expired token → raw errors, no redirect.
- **P6** `Name` ≤ 15 and `Email` ≤ 25 chars (Plan §3.1 `[SPEC]`, conflicts with `StationerySchema.sql` — K2). 25 rejects most real emails.
- **P7** Dev connection string targets LocalDB; this machine has SQLEXPRESS only.

## 8. Frontend problems

No nav entry for `/new-request` · Draft illusion (C1) · "Fulfilled" filter option unreachable (C8) · `PagePlaceholder` only serves Help · `pages/NewRequest.jsx` / `MyRequests.jsx` are one-line re-export shims · no supplier-order list · no cancellation decision UI (C5).

## 9. Backend problems

`ApprovalController` uses `try/catch` (CLAUDE.md #2 forbids; middleware already maps exceptions) · no `RequestStateMachine` · dead code: `Service.cs`/`IService.cs`, `DbSeeder.SeedDemoDataAsync` (comment claims a `Seed:DemoData` gate that doesn't exist), `ApproveCancellationCommandValidator`, `IssueAsync`, `modifiedCount` · `GetUsersAsync` loads all users before paginating · `Comment` optional on rejection (Plan §3.6 requires) · two concurrency strategies (`Request` uses `IsConcurrencyToken()`, items/suppliers don't).

## 10. Database problems

No column for per-line approval decision (C2) · `CK_Requests_Status` lacks `Draft`, has unreachable `Fulfilled` · `ApplicationUser.RankLevel` is never maintained (C3) · `SupplierRequest` has no status (deliberate, K3) · no FK from `StockTransactions` to a request · `CK_StationeryItems_QuantityAvailable >= 0` in SQL file but not in EF · `StationerySchema.sql` no longer describes the DB (K2/K3/K8).

## 11. Role & permission problems

Manager-only pages: correct on all three layers. Reports: contradicts spec (P1). Manager-wide request visibility: broken (C3). Delete request: UI-only guard (C4). Role assignment: escalation path (P2).

## 12. AI feature — **requirement satisfied**

`RequestAssistantService`: grounded in the caller's rank-filtered catalogue, strict JSON, 10 s timeout + 1 retry, keyword fallback works offline, every field validated, LLM writes only its own log row, key from env/user-secrets only, rate-limited 20/user/hour. Watch: `CatalogueLoadLimit = 500`.

## 13–14. Code explanation and lessons

See the interactive report (link at top) — sections 13 and 14.

## 15. Recommended next steps

1. **C3** — copy `ReportQueries.ResolveScopeAsync`'s role join into `RequestQueries` (30 min, zero risk).
2. `RequestStateMachine` encoding Plan §3.6, then **one migration**: `Draft` in the status check; `Decision` + `ApprovedQuantity` on `RequestItems`; nullable `RequestId` on `StockTransactions`.
3. In order: C1 → C2 → C4 → C8 (call `IssueAsync` behind a stock guard) → C7.
4. C5 + C6 together.
5. Team decisions: P1, P2, K2/K3.
6. Tests: Plan §3.6 transition table as a `[Theory]`; one `MigrateAsync()` test on real SQL Server; regressions per fixed finding.
7. Cleanup: update `CLAUDE.md` §1; delete/wire dead code; nav entry for New Request; 401 interceptor; N+1; `bin/`+`obj/` in `.gitignore`.

Do **not**: move `RequestService` to `Application`; add MediatR/AutoMapper/UnitOfWork/SignalR (Plan §2.4); rewrite the AI feature.
