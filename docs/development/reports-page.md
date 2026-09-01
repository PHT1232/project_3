# Reports Page — Implementation Handoff

> **2026-08-31 update — real backend + role scoping.** Sections 1–9 below (up to "Post-merge")
> describe the original **mock-data, Manager+-only** version of this page. As of this update
> that description is superseded: the backend is real (`WebApi/Controllers/ReportsController.cs`
> + `Infrastructure/Queries/ReportQueries.cs`), the page is open to every authenticated user
> (`[Authorize]`, not `RequireManager`), and every tab's data is now **row-scoped per user**.
> Read **§10 onward** first; §1–9 are kept for the front-end-only history (component structure,
> print/CSV/sort mechanics — those parts are still accurate) but their data-flow and gating
> claims are not.

## 1. What the page does

Six tabs (five date-ranged + one always-on personal tab), each a distinct view (page-map §9:
"three distinct views, not one page with three columns" — extended here to six):

| Tab id | Label | Source | Visible to | Date-filtered? |
|---|---|---|---|---|
| `MY_REQUESTS` | My Requests | `getMyActivityReport` | **everyone**, always | yes |
| `COST_BY_ITEM` | Cost by Item | `getCostByItemReport` | everyone | yes |
| `HEADCOUNT` | Cost & Headcount | `getItemHeadcountReport` | everyone | yes |
| `CUMULATIVE` | Cumulative Cost | `getCumulativeCostReport` | everyone | yes |
| `INVENTORY_VALUATION` | Inventory Valuation | `getInventory` (inventory API) | Manager+ (rank ≥ 2) | **no — point-in-time** |
| `BY_TEAM` | By Team | `getTeamExpenditureReport` | Business Manager+ (rank ≥ 3) | yes |

All money figures are over requests in a committed state (`Approved` / `PartiallyApproved` /
`Fulfilled`) and use `RequestItems.LineTotal` (the `UnitCostSnapshot` × quantity taken at
submission — never the live `StationeryItems.UnitCost`) — Plan §3.4 / page-map §9.

Cross-cutting UI (unchanged from the mock version):
- **ReportMetaBar** — `Generated: … · Period: … · Approved requests only · Manager view`. On
  `INVENTORY_VALUATION` the period is replaced by `As of <today>`.
- **ReportInsight** (new, §11) — one computed sentence above the KPI tiles, e.g. *"Your team's
  spend is up 22.0% versus the previous period, mostly driven by A4 Copy Paper."*
- **Export CSV / Print** buttons. Both pure frontend; unaffected by this change except that
  `MY_REQUESTS` now also has a CSV export.
- **Sortable columns**, **Search + Category + Approved-cost filters** on the two item tabs —
  unchanged.

## 10. Why this changed: role-scoped data, server-side

**The ask:** a Requestor should only ever see their own spend; a Manager only their direct
team; a Business Manager their group (their managers + those managers' teams); the Managing
Director everything — enforced so that out-of-scope data is **never returned by the API**, not
merely hidden in the UI. Plus a personal "My Requests" tab that's always visible (a manager is
also a requestor), and a one-line computed insight per tab.

That could not be done as a client-side filter over `reports.mock.js`, because there was no
backend at all — every number came from a deterministic mock generator shipped whole to the
browser. Satisfying "never returned by the API" required building the API. See the plan
discussion in the conversation this shipped from for the options considered; the team chose
"build the real backend now."

### Existing role/permission model this reuses (did not invent a new pattern)

- **Rank** comes from the Identity role assigned to the user (`AspNetUserRoles` →
  `AspNetRoles.RankLevel`: Engineer=1, Manager=2, Business Manager=3, Managing Director=4) —
  **not** `ApplicationUser.RankLevel`, which is a vestigial column nothing in the real
  auth/user-creation path (`IdentityAccountAdapter`, `IdentityUserStore`) ever populates. This
  was caught by testing against real seeded/created users rather than trusting the column name
  (see CLAUDE.md K8) — `ReportQueries.ResolveScopeAsync` reads rank the same way `/auth/me`
  and the JWT `rankLevel` claim do.
- **Hierarchy** = `ApplicationUser.SuperiorEmployeeNumber` (self-referencing, `null` = top),
  the same field `RequestQueries` already uses for "is this an approver" and "whose requests
  can I see."
- **Authorization**: still `[Authorize]` on the controller (row-level scope is the real
  control per CLAUDE.md principle #9) plus `ICurrentUserService.EmployeeNumber` passed into
  the query layer — the exact pattern `RequestsController` / `RequestQueries` established.

### Scope resolution (`Infrastructure/Queries/ReportQueries.ResolveScopeAsync`)

| Scope | Who | Requestor IDs included |
|---|---|---|
| `Self` | No direct reports (any rank) | just the caller |
| `Team` | Rank 2 approver (Manager) | caller + direct reports |
| `Group` | Rank 3 approver (Business Manager) | caller + direct-report managers + **those managers' own direct reports** (fixed two levels — deliberately not a recursive descendant walk) |
| `Org` | Rank ≥ 4 (Managing Director) | everyone (no filter) |

`GetMyActivityAsync` (My Requests) ignores scope entirely and always filters to `[actor]` —
the one report that's the same for every role.

**Verified nesting** (integration + live smoke test against seeded demo data): Engineer's
`Self` total ⊆ their Manager's `Team` total ⊆ that Manager's Business Manager's `Group`
total, and a sibling Business Manager's group never appears. `By Team` for a Business Manager
correctly excludes a sibling Business Manager's teams.

### A deliberate architecture deviation, flagged rather than silent

`IReportQueries`'s original doc comment (and the Plan generally) calls for SQL-side
`GROUP BY`. The implementation here does the **scope + date filter in SQL** (only the
caller's rows are ever fetched) but the **aggregation** (by item / month / manager, `DISTINCT`
counts) runs **in memory** over that already-scoped row set. Reason: composing `GroupBy` with
the `RequestItem → Request`/`StationeryItem`/`Category` navigation joins this needs does not
translate on SQLite — the integration-test provider Plan §10.2 mandates over the EF `InMemory`
provider for FK/transaction fidelity — only on SQL Server, which would make the test suite
lie about what ships. Given the bounded data volume (an eProject's request history, not a
multi-tenant table), in-memory aggregation over the scoped set is correct and portable, and
matches the precedent already in this codebase (`InventoryQueries.GetSummaryAsync` does the
same). Documented in `ReportQueries`'s class-level doc comment.

## 11. The insight line

Each report DTO (except Inventory Valuation, which has no backend endpoint) carries an
`Insight` field (`ReportInsightDto`) computed server-side from **the same scoped dataset**,
plus one extra query against the immediately-preceding equal-length window — no new data
source, per the brief:

- **`PeriodDelta`** (Cost by Item, Cumulative, By Team, My Requests): current vs previous
  window total, `%` change, and the item whose spend rose the most between the two windows
  (`DriverLabel`). `ChangePercent` is `null` when the previous window had zero spend (nothing
  to compare against — the sentence says so explicitly rather than showing "∞%" or "0%").
- **`Composition`** (Cost & Headcount): the top item's share of the period's total spend and
  the distinct-requestor count — a concentration statement rather than a trend, since
  headcount isn't really a "went up/down" number.
- **`Empty`**: no committed spend in the window.

`frontend/src/lib/insights.js` turns the DTO into the sentence (unit-tested,
`insights.test.js`); `ReportInsight.jsx` just renders whatever string it's given.
`INVENTORY_VALUATION` has no `ReportInsightDto` (it isn't date-ranged), so `ReportsPage`
builds its sentence client-side from the inventory rows already fetched
(`buildInventoryInsightSentence` — e.g. *"$14,520 tied up in stock; 2 items are at or below
reorder level."*).

## 12. Tab visibility vs. the real control

`ReportsPage.ALL_TABS` filters by `user.rankLevel` from `AuthContext` — `INVENTORY_VALUATION`
needs rank ≥ 2 (unchanged), `BY_TEAM` needs rank ≥ 3 (new: a plain Manager has exactly one
team, so the comparison view is meaningless for them). **This is UX only.** Calling
`/api/v1/reports/by-team` directly as a Manager or an Engineer does not error — it returns
their own correctly-scoped (thin) view, never someone else's data. Route-level gating
(`ProtectedRoute requireManager` on `/reports`) was **removed** — the whole page is now
`[Authorize]` only, because a plain Requestor is a legitimate audience (their own spend + My
Requests). `navigation.js`'s Reports entry lost its `minRankLevel: 2`.

## 13. Demo seed data (`Infrastructure/Data/DbSeeder.SeedDemoDataAsync`)

Dev-only, gated behind `Seed:DemoData=true` (set in `appsettings.Development.json`), called
from `Program.cs` right after the existing bootstrap-admin seed. Idempotent — skips entirely
if employee `#20` already exists. Builds, under the existing bootstrap MD (`#1`):

```
MD (#1, pre-existing)
 ├─ Business Manager #20 "Nora Vance" ── Manager #22 "Amy Cole" ── Engineers #26,27,28
 │                                    └─ Manager #23 "Ben Frost" ── Engineers #29,30,31
 └─ Business Manager #21 "Paul Reeves" ─ Manager #24 "Cara Diaz" ── Engineers #32,33,34
                                       └─ Manager #25 "Dan Webb" ── Engineers #35,36,37
```

Plus a 15-item / 4-category catalogue (only if the DB has fewer than 8 active items already)
and 100 synthetic requests (deterministic RNG, seed `20260830`) spread over the last ~150
days, weighted toward `Approved`/`Fulfilled` with some `PartiallyApproved`/`Rejected` mixed in
so the "approved-only" filter is actually exercised. All demo accounts share the bootstrap
admin's password (`Seed:BootstrapAdminPassword`).

**Not idempotent against re-seeding with different data** — if you need to reset it, the
demo employee numbers (`#20`–`#37`) and their requests would need manual deletion first;
there's no `--reset` flag.

## 14. Files (this change)

**New**
- `Application/DTOs/Reports/*.cs` — `ReportInsightDto` (+ `ReportScope` enum),
  `CostByItemReportDto`, `ItemHeadcountReportDto`, `CumulativeCostReportDto`,
  `TeamExpenditureReportDto`, `MyActivityReportDto`.
- `Application/Interfaces/Reports/IReportQueries.cs`
- `Infrastructure/Queries/ReportQueries.cs`
- `WebApi/Controllers/ReportsController.cs`
- `Tests/WebApi.IntegrationTests/ReportsTests.cs` — the scope-boundary matrix (6 tests).
- `frontend/src/lib/insights.js` (+ `insights.test.js`)
- `frontend/src/pages/reports/components/ReportInsight.jsx`
- `frontend/src/pages/reports/components/MyActivityView.jsx`

**Modified**
- `WebApi/Program.cs` — registers `IReportQueries`; calls `SeedDemoDataAsync` when
  `Seed:DemoData=true`.
- `WebApi/appsettings.Development.json` — `Seed:DemoData: true`.
- `Infrastructure/Data/DbSeeder.cs` — `SeedDemoDataAsync`.
- `frontend/src/api/reports.js` — real `client.get('/reports/…')` calls (mock deleted);
  `getMyActivityReport` added.
- `frontend/src/lib/reports.js` — trimmed to `resolveRangeFromPreset` / `defaultReportBounds`;
  the aggregation functions moved server-side (`reports.test.js` rewritten to match).
- `frontend/src/pages/reports/ReportsPage.jsx` — `useAuth()`-driven tab visibility, `MY_REQUESTS`
  tab + fetcher + CSV export, `ReportInsight` rendering, bounds no longer sourced from mock data.
- `frontend/src/App.jsx` / `navigation.js` — `/reports` route and nav entry no longer
  Manager+-gated (moved out of the `requireManager` route group; `minRankLevel` removed).

**Deleted**
- `frontend/src/api/mock/reports.mock.js`

## 15. Tests actually run (this change)

- `dotnet build Project.slnx` — 0 errors.
- `dotnet test Project.slnx` — **76 passed** (26 `Application.UnitTests` + 50
  `WebApi.IntegrationTests`, including the new 6-test `ReportsTests` scope matrix).
- `npm run build` — pass (Vite, 1699 modules).
- `npm test` — **64 passed** (13 files), including `insights.test.js` (7 cases) and the
  rewritten `reports.test.js` (date-helper tests only now).
- **Live smoke test** against the seeded demo hierarchy (real HTTP calls, real JWTs, real SQL
  Server): Engineer `#26` (`Self`, $1,111.67) ⊂ Manager `#22` (`Team`, $10,945.22) ⊂ Business
  Manager `#20` (`Group`, $23,154.94); `By Team` for `#20` lists 4 teams summing to the group
  total with no leakage from Business Manager `#21`'s group; `My Requests` for each of those
  three returns a different, smaller number than their scoped report (proving it's genuinely
  self-only, not an alias for the scoped view).
- **Not done:** no browser click-through by the developer of the finished page (module
  resolution + live API smoke test only); Managing Director scope verified via the integration
  test's synthetic hierarchy, not the live seeded DB (the pre-existing `#1` account's password
  is unknown to this session).

## 16. Assumptions & known gaps (this change)

- **Business Manager "Group" scope is fixed-depth-2, not recursive** — an explicit, confirmed
  product decision (a true recursive descendant walk was considered and rejected for this
  eProject's timeline). If the org hierarchy ever grows a third management layer, `Group`
  scope will under-report for the top Business Manager tier.
- **"Committed spend" = `Approved` ∪ `PartiallyApproved` ∪ `Fulfilled`** — the Plan says
  "Approved only"; the status enum splits committed money into three terminal states. Flagged,
  not silently narrowed to literally `Approved`.
- **`ReportScope` serializes as a string** (`JsonStringEnumConverter`) for readability; the
  frontend does not currently read it (tab visibility comes from `AuthContext`, independently).
- **Insight `DriverLabel` is item-level for every `PeriodDelta` report**, including `By Team`
  — i.e. "driven by [item]" even on the team-comparison tab, not "driven by [team]". Simpler
  and still accurate; revisit if a team-level driver reads better in practice.
- **No UI regression test** for tab visibility by role or for the insight banner's rendering;
  covered by the backend integration tests and the `insights.js` unit tests, not a component
  test.
- `#1`'s (pre-existing "Diana Director") password is unknown to this session, so the `Org`
  scope was validated via the integration test's own MD user, not live against seeded data.

## 17. Reviewer follow-ups

- Confirm the fixed-depth-2 Business Manager scope reading (§10) is what was intended, not a
  stricter one-hop-only reading.
- Confirm "committed spend" status set (§16).
- Decide whether `SeedDemoDataAsync` should ship long-term (it's dev-only and gated, but the
  hierarchy is invented, not real HR data) or be replaced once real users exist in every tier.
- Click through the app as an Engineer, a Manager, a Business Manager, and the MD and confirm
  the tabs shown and numbers match expectations — this session verified via HTTP, not the
  rendered UI.

---

*Sections 1–9 below are the original mock-data-era handoff, kept for the parts that are still
accurate (component structure, print CSS, sort/filter mechanics). Their data-flow diagram and
"Manager+ gated" / "runs on mock data" claims are superseded by §10–§16 above.*

## 2. Data flow (superseded — see §10)

```
reports.mock.js  (deterministic seed: ~95 approved requests / ~178 lines, fixed PRNG+seed)
   │  MOCK_APPROVED_REQUEST_LINES, MOCK_DATA_BOUNDS, MOCK_TEAM_MAP
   ▼
lib/reports.js  (pure aggregations — unit-tested in lib/reports.test.js)
   │  buildCostByItem · buildItemHeadcount · buildCumulativeCost(+topConsumed) · buildTeamExpenditure
   │  filterLinesByRange · distributeTo100 · resolveRangeFromPreset
   ▼
api/reports.js  (mock fetchers; each returns a `kind` discriminator + `range`)
   ▼
ReportsPage.jsx  (two useAsync hooks: one for the date-range FETCHERS map, one for inventory
   │  that only fires when its tab is active; `kind` guard prevents stale cross-tab render)
   ▼
component views  (CostByItemView, ItemHeadcountView, CumulativeCostView,
                  InventoryValuationView, TeamExpenditureView)
```

**`kind` guard:** after a tab switch `useAsync` still holds the previous payload for one
render. `ReportsPage` computes `report = main.data?.kind === tab ? main.data : null` so the
wrong view never renders against the wrong shape (this was a real white-screen bug — see
`AI_usage_report.md` 2026-08-28). **This guard is still in place and still load-bearing** —
only the data source behind `api/reports.js` changed.

**Client-side filtering** (`applyReportFilters`, item tabs): narrows `report.rows`; the table
footer then shows the shown subset, and the `% of Total` column keeps meaning "share of the
full period spend" (a note is shown when a filter is active). `distributeTo100` (now
server-side) still guarantees the unfiltered column sums to exactly 100.00 (TC-16).

**Sorting** (`tableSort.js` + `SortHeader.jsx`): each view holds its own `sort` state
(`{ key, dir } | null`); `applySort` reorders the already-filtered rows. Footer totals are
unaffected (sort changes order, not membership).

## 5. Printing

`ReportsPage` injects a `<style id="reports-print-style">` into `<head>` on mount (removed on
unmount). `@media print`: `body * { visibility: hidden }` then `[data-print-region] *` visible
— this hides the sidebar and top header **without modifying those components** (they are out
of scope). `[data-print-hide]` (tabs, toolbar, date control, Export/Print buttons, charts)
is `display:none`; tables are forced black-on-white; a `[data-print-footer]` "HMT
Technologies … — Confidential" line prints at the bottom.
