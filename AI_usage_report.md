# AI Usage Report

## 2026-08-17 — Stationery Request Management schema (SQL)

**Prompt:** User supplied a Mermaid `erDiagram` (Roles, RoleThresholds, Users, Suppliers,
Categories, StationeryItems, Requests, RequestItems, RequestStatusHistory, Notifications,
StockTransactions, AiInteractionLogs) and asked for it to be turned into a SQL file.

**What was done:**
- Read `__ai_agents/systemprompt.md`, `backend.md`, `frontend.md` for architecture/stack context (.NET 10, EF Core, SQL Server).
- Found an existing draft schema at `__ai_agents/Database/Project 3.sql` (earlier, less normalized version of the same domain) and used its file conventions (bracketed identifiers, `GO` batch separators, FKs added via `ALTER TABLE` after table creation) for consistency.
- Wrote `__ai_agents/Database/StationerySchema.sql`: full T-SQL DDL for the 12 entities in the diagram, with primary keys, foreign keys, unique constraints, and default constraints derived from the diagram's cardinalities and field types.

**Assumptions made (flagged for review, not silently decided):**
- `Requests.Status` and `StockTransactions.TxType` got `CHECK` constraints with a guessed set of enum values (`Pending/Approved/Rejected/PartiallyApproved/Cancelled/Fulfilled` and `Inbound/Outbound/Adjustment/Return`) since the diagram only specified the columns as `nvarchar`, not their allowed values.
- All foreign keys referencing `Users` use `ON DELETE NO ACTION` — SQL Server disallows multiple cascade paths converging on one table (five different FKs point at `Users`: superior, requestor, approver, recipient, actor, invoker), and cascading deletes through employee/audit history is generally unsafe. `IsActive` is the intended way to deactivate a user.
- `RequestItems.LineTotal` was kept as a stored column (per the diagram) rather than a computed column — the diagram lists it as a plain `decimal` attribute, so keeping `Quantity * UnitCostSnapshot` in sync is left to the Application layer.
- `Notifications.RequestId` and `StockTransactions.RequestId` were made nullable to allow for notifications/stock movements not tied to a specific request (e.g. manual restocks). This wasn't explicit in the diagram's cardinality notation.
- Did not overwrite the existing `Project 3.sql` draft — created a new file instead so both can be diffed before deciding which supersedes the other.

**Not yet done:** No EF Core entity classes, migrations, or `DataContext` configuration were generated — this pass only covers the raw SQL DDL requested.

## 2026-08-24 — Align backend.md / frontend.md with the Project Plan

**Task:** Read `__ai_agents/systemprompt.md` and `__ai_agents/Stationery_Management_System_Project_Plan.md`, then rewrite `__ai_agents/backend.md` and `__ai_agents/frontend.md` so the standing architecture context matches the plan's decisions instead of the generic placeholder text they previously held.

**What changed, by file:**
- `__ai_agents/backend.md` — rewritten. Corrected the .NET version claim (was ".NET 10", plan's `[DECISION]` is **.NET 8 LTS**); added the rejected-patterns list (no MediatR/CQRS, no UnitOfWork wrapper, no AutoMapper, no SignalR, no blanket soft-delete); documented the header/line request split, stock-as-ledger, request state machine, cross-cutting concerns table, API conventions, the AI feature's server-side/never-writes-to-DB/fallback rules, coding standards, and testing approach — all sourced from plan §2–§10.
- `__ai_agents/frontend.md` — rewritten. Flagged that the current `frontend/` scaffold (bare Vite JS, `main.js`/`counter.js`) is not yet the target React app described in the plan. Added component conventions, the `AuthContext`/no-Redux state decision, the documented `localStorage` JWT trade-off (plan §9.2, explicitly a conscious choice, not an oversight), key screens list, and testing scope.
- Read-only: `__ai_agents/Stationery_Management_System_Project_Plan.md` (full file), `__ai_agents/systemprompt.md`, and did a repo-structure check (`find`) to confirm the current backend/frontend scaffolding still matches the plan's described M0 starting state.

**Assumptions made:** None requiring flagging — both files were rewritten as direct summaries of decisions already made explicitly in the plan (labelled `[DECISION]`/`[SPEC]`/`[RUBRIC]` therein), not new judgment calls.

**Left out of scope:** No code was changed — this was a documentation-only pass. Did not touch `README.md`'s .NET version claim or delete the `WeatherForecastController`/`Class1.cs` scaffolding, even though the plan calls both out as M0 cleanup items, since the user's request was specifically to update `backend.md`/`frontend.md`.

## 2026-08-27 — Plan full ASP.NET Core Identity authentication and user management

**Task:** Pull the latest repository state and create an implementation plan for sign-in, logout, and user management using the full ASP.NET Core Identity framework. Add a permanent rule requiring an AI usage entry and a detailed Markdown handoff after implementation.

**What changed, by file:**
- `CLAUDE.md` — added the mandatory completion-documentation rule: append to `AI_usage_report.md` and create/update a task-specific handoff in `docs/development/` after implementation.
- `docs/development/identity-and-user-management-implementation-plan.md` — added the implementation plan for Identity-backed users and roles, JWT sign-in, client-side logout, authorization policies, user-management API/UI, tests, delivery order, and documentation requirements.
- `AI_usage_report.md` — appended this documentation-only planning record.

**Architecture decision:** Use full ASP.NET Core Identity stores and managers in `Infrastructure` (`IdentityUser<int>`, `IdentityRole<int>`, `IdentityDbContext`, `UserManager`, `RoleManager`, and `SignInManager`) while retaining JWT bearer authentication for the API. Identity and EF types do not enter `Core` or `Application`; Application uses project-owned interfaces and DTOs. JWT issuance remains project-owned because Identity does not issue JWT access tokens.

**Assumptions and open decisions:**
- Plan proposes using Identity's integer user `Id` directly as the employee number, configured as `ValueGeneratedNever()`, to preserve the required 1–1000 primary key/login contract.
- Initial-password policy is not explicitly specified; the proposed default matches change-password rules (minimum 8, mixed case, digit) and needs confirmation before implementation.
- The `/users` location filter remains `NOT SPECIFIED` until its data representation is confirmed.
- Immediate invalidation of JWTs after deactivation is proposed through active-user validation in `OnTokenValidated`; this must be documented because JWTs are otherwise valid for up to eight hours.

**Validation actually run before planning:**
- Pulled `main` from `95b4553` to `d64e333` with a fast-forward.
- `dotnet build Project.slnx --no-restore` passed using SDK `10.0.111`; existing warning `NU1903` reports a high-severity vulnerability in `Microsoft.OpenApi` 2.0.0.
- `npm run build` did not run because frontend dependencies are absent: `vite: command not found`.

**Left out of scope:** No authentication, Identity model, API, migration, frontend feature, or test was implemented in this task. No secrets or connection strings were added. Existing generated `bin/` and `obj/` changes were not removed or overwritten.

## 2026-08-27 — Implement sign-in, logout, authorization, and user management

**Task:** Execute `docs/development/identity-and-user-management-implementation-plan.md` end to
end: Identity model + migration, login/`/auth/me`/JWT bearer, frontend auth context/login/logout,
`RequireManager`/`RequireApprover` policies + active-user enforcement, user-management CRUD API,
user-management UI, `POST /auth/change-password`, and backend + frontend tests. User asked for
"everything now, no pauses" with a commit after each delivery step.

**What changed, by file (grouped by delivery step / commit):**
- **Identity model + migration** — `Infrastructure/Identity/{ApplicationUser,ApplicationRole}.cs`,
  `Infrastructure/Data/Configurations/*`, `Infrastructure/Data/DbSeeder.cs`,
  `Infrastructure/Data/Migrations/20260827133027_InitialIdentity*`, `Infrastructure/DataContext.cs`
  (now `IdentityDbContext<ApplicationUser,ApplicationRole,int>`), `WebApi/Program.cs` (DbContext +
  Identity registration), `WebApi/appsettings*.json` (connection string / JWT config keys).
- **Backend auth** — `Application/{DTOs,Interfaces,Services}/Auth/*`, `Application/Exceptions/*`,
  `Infrastructure/Identity/{IdentityAccountAdapter,JwtTokenService}.cs`,
  `WebApi/Controllers/AuthController.cs`, `WebApi/Middleware/ExceptionHandlingMiddleware.cs`.
- **Frontend auth** — `frontend/src/api/{auth,client}.js`, `frontend/src/contexts/AuthContext.jsx`,
  `frontend/src/routes/ProtectedRoute.jsx`, `frontend/src/pages/Login.jsx` (real form, replacing
  the placeholder), `frontend/src/components/layout/Header.jsx` (account menu + logout),
  `frontend/src/App.jsx`/`main.jsx`. Deleted `pages/SignUp.jsx` and `components/AuthPlaceholder.jsx`
  — `/signup` now redirects to `/login`.
- **Authorization** — `WebApi/Authorization/{RankLevelRequirement,ApproverRequirement}.cs`,
  `WebApi/Services/CurrentUserService.cs`, `Application/Interfaces/Auth/ICurrentUserService.cs`,
  `OnTokenValidated` active-user check in `Program.cs`.
- **User-management API** — `Application/{DTOs,Interfaces,Services,Validators}/Users/*`,
  `Infrastructure/Identity/IdentityUserStore.cs`, `WebApi/Controllers/UsersController.cs`.
- **User-management UI** — `frontend/src/pages/users/*` (replaces `pages/UserManagement.jsx`),
  `frontend/src/api/users.js`, nav gating in `navigation.js`/`Sidebar.jsx`.
- **Change password** — `Application/DTOs/Auth/ChangePasswordRequest.cs`,
  `Application/Interfaces/Auth/IPasswordService.cs`,
  `Application/Validators/Auth/ChangePasswordRequestValidator.cs`,
  `Infrastructure/Identity/IdentityPasswordService.cs`, `AuthController.ChangePassword`.
- **Tests** — `Tests/Application.UnitTests/*` (17 tests), `Tests/WebApi.IntegrationTests/*`
  (13 tests, `WebApplicationFactory<Program>` + real EF Core SQLite in-memory), 4 new frontend
  Vitest/RTL files (15 tests). `WebApi/appsettings.Testing.json` added for the integration tests'
  JWT config. `Program.cs` now calls `Database.MigrateAsync()` outside the `Testing` environment
  (previously the step-1 migration was generated but never applied anywhere).
- **Docs** — this entry; `.gitignore` gained a narrow `Tests/**/bin|obj` rule after an initial
  `git add Tests/` accidentally staged 635 build-artifact files (caught and reverted in a follow-up
  commit before anything else was staged on top of it).

**APIs added:** `POST /api/v1/auth/login`, `GET /api/v1/auth/me`, `POST /api/v1/auth/change-password`,
`GET/POST /api/v1/users`, `PUT /api/v1/users/{empNo}`, `PATCH /api/v1/users/{empNo}/status`,
`GET /api/v1/users/{empNo}/subordinates`.

**DB changes:** One EF Core migration (`InitialIdentity`) — the 7 standard ASP.NET Identity tables
(`AspNetUsers` carrying the domain fields `Name`/`Grade`/`Location`/`SuperiorEmployeeNumber`/
`IsActive`/`CreatedAtUtc`, plus `AspNetRoles` with `RankLevel`), a `CK_Users_EmployeeNumber`
check constraint (1–1000), and a self-referencing FK for `SuperiorEmployeeNumber`. Not yet applied
to a real SQL Server instance — no SQL Server was available in this environment; verified instead
via the SQLite-in-memory integration tests and `dotnet ef migrations has-pending-model-changes`
(reports none).

**Tests actually executed:**
- `dotnet build Project.slnx` — succeeds (1 pre-existing `NU1903` warning, unrelated).
- `dotnet test Project.slnx` — **30/30 passed** (17 unit + 13 integration).
- `npx vitest run` (frontend) — **15/15 passed**.
- `npm run build` (frontend) — succeeds.
- Manual smoke test against a real running server/browser was **not** performed — no SQL Server
  instance was available, so the API was never run end-to-end outside the test suite. Do not
  treat this as "verified in the browser."

**Two real bugs the tests caught (not just exercised):**
1. `JwtBearerOptions.MapInboundClaims` defaulted to `true`, so the handler silently remapped the
   `sub` claim on every validated token. This broke `/auth/me`, `ICurrentUserService`, and the
   `RequireApprover` handler for every authenticated request after login — not a test-only issue.
   Fixed by setting `MapInboundClaims = false`.
2. `AuthContext`'s restore effect depended on `token`, so `login()` setting the token re-triggered
   a redundant `/auth/me` fetch immediately after login (crashed in tests where `fetchCurrentUser`
   wasn't mocked; in production it's a wasted network call and a spurious `restoring` flicker).
   Fixed by running the restore effect once on mount only.

**Assumptions made (flagged, not silently decided):**
- Initial-password and change-password policy: minimum 8 characters, upper+lower+digit — proposed
  in the plan, not confirmed by the team. Still unconfirmed.
- `RequireApprover` implemented as a live "has direct reports" check (resolved as part of the
  implementation-plan review, see `identity-and-user-management-implementation-plan.md` §6),
  not a precomputed claim.
- `location` filter on `GET /users` is implemented against `Users.Location` (present in
  `StationerySchema.sql`), but whether that column is Plan-sanctioned is still open — see K5.

**Explicitly left out of scope:**
- **TC-14 (change-password notifications) is NOT complete.** `POST /auth/change-password` changes
  the password and rotates the security stamp; it does **not** notify the user and their superior,
  because no notification infrastructure exists yet (Plan M4). Documented in a code comment on
  `AuthService.ChangePasswordAsync` so this isn't silently claimed as done later.
- No EF migration was applied to a live SQL Server database — none was available in this
  environment. The migration is generated and reviewed but unexecuted outside tests.
- `RoleThresholds` (spend-limit table) was not created — out of scope for auth/user management.
- The `Grade` and `Location` columns exist on `ApplicationUser` and round-trip through the API/UI,
  but their Plan-sanctioned status is still open per K5/K8 in `CLAUDE.md`.
- K8 (Identity-vs-custom-auth) is logged as **closed** in `CLAUDE.md` §6 with the user's 2026-08-27
  sign-off, but the Plan document itself (`__ai_agents/Stationery_Management_System_Project_Plan.md`)
  still describes the original custom-auth design and was not revised — flagged there for the next
  Plan edit, same as how K7 (.NET version) was eventually closed.

**Shared files touched:** `frontend/src/App.jsx`, `navigation.js`, `Sidebar.jsx`, `Header.jsx`,
`api/client.js`, `Project.slnx`, `.gitignore`, `CLAUDE.md` (K8 entry, prior commit),
`WebApi/Program.cs`.

**Reviewer follow-ups:**
- Confirm the initial/change-password policy (8+ chars, mixed case, digit) with the team, or
  replace it if the Plan specifies something else.
- Decide whether `RequireApprover`'s live DB check is acceptable long-term or should become a
  claim set at login (noted as an open question in the implementation-plan doc).
- Reconcile `Grade`/`Location` and the Identity table footprint against the 12-table ERD (K8
  follow-up) before the next schema-touching PR.
- Run the migration against a real SQL Server instance and do a manual browser smoke test before
  merging — neither happened in this session.

## 2026-08-28 — Catalogue unit-cost filter: value badge above the slider thumb

**Task:** Add a floating value badge (speech-bubble/callout) pinned above the "Unit Cost Max"
range slider thumb on the catalogue filter panel, so the exact numeric ceiling (e.g. `$52`,
`$100+`) is visible while dragging.

**What changed, by file:**
- `frontend/src/pages/catalogue/components/CatalogueFilters.jsx` — extracted the inline
  `<input type="range">` into a `UnitCostSlider` sub-component (mirrors the existing `FieldSet`
  helper convention in the same file). Added a `pointer-events-none`, `aria-hidden` badge
  positioned with `left: calc(<percent>% + <thumbOffset>px)` where `percent = cost / MAX_COST_CAP`
  and `thumbOffset = (0.5 − percent/100) × 16px` keeps it centred over the native thumb at both
  ends. Badge styled with the existing `brand-700` token (matches the slider's `accent-brand-700`),
  a CSS rotated-square arrow, `tabular-nums`, and a short `transition-[left]`. Added
  `aria-valuetext` on the input so screen readers announce `$52` / `$100+` instead of the raw
  number. No behaviour/logic change to filtering; `filters.js` untouched.

**Assumptions made (ambiguous, flagged for review):**
- Native range-thumb width assumed ≈16px for the centring offset. Browsers vary ~14–18px; the
  badge is centre-anchored so the residual error is sub-pixel visually. Not overridable without a
  custom-styled thumb (out of scope).
- Label at the cap shows `$100+` (open-ended), consistent with the existing track label and
  `describeActiveFilters` chip wording. Currency symbol stays `$` — the unresolved VND/`[ASK] #10`
  decision lives in `lib/format.js`; this badge deliberately does not introduce a second currency
  source (it builds the string from `MAX_COST_CAP`, no new symbol inlined).

**Validation actually run:**
- `npm run build` — passed (Vite 8.2.2, 1671 modules, no errors/warnings).
- `npm test` — 4 files / 15 tests passed (no test covers `CatalogueFilters`; none added this pass).
- Vite dev-server HMR applied the change cleanly; not yet manually eyeballed in a browser.

**Left out of scope:** No new component test for the badge/offset maths. No change to the
mock-data catalogue, `filters.js`, currency handling, or the disabled "Available to Me" radio.

## 2026-08-28 — Reports page (Manager's cost report) — frontend / UI half

**Tool:** Claude Code (claude-sonnet-5).

**Task:** Build the Reports page (`/reports`, Manager+) in the SPA — the three cost reports
from Plan §4.2/§5 and page-map §9: Cost by Item (+ % of total), Cost & distinct-requestor
Headcount, Cumulative Cost over time — with a date-range filter. Backend (`GET /reports/*`,
M3's SQL work) does not exist, so this follows the established mock-backed frontend pattern
(`catalogue.js`/`inventory.js`).

**What AI produced, by file:**
- `frontend/src/lib/reports.js` (new) — pure aggregations: `buildCostByItem`,
  `buildItemHeadcount`, `buildCumulativeCost`, `distributeTo100` (shares forced to sum to
  exactly 100.00 as `100 − Σ(others)`, per TC-16), `filterLinesByRange`,
  `resolveRangeFromPreset`.
- `frontend/src/lib/reports.test.js` (new) — 10 Vitest cases: 100.00% sum incl. rounding
  residual, distinct-requestor count (TC-17 analogue), cumulative monotonic + reconciles with
  the cost-by-item total, inclusive range filter, preset→range resolution.
- `frontend/src/api/mock/reports.mock.js` (new) — TEMPORARY. Deterministic generator
  (mulberry32, fixed seed) → 95 approved requests / 178 lines over ~120 days, 15 items with
  names/costs copied from `catalogue.mock.js`/`inventory.mock.js`. Delete when `GET /reports/*`
  lands.
- `frontend/src/api/reports.js` (new) — documents the expected Manager+ / Approved-only /
  `?fromDate=&toDate=` contract for all three endpoints; mock bodies call the `lib/reports.js`
  builders so the date picker actually filters. Commented `client.get(...)` lines for go-live.
- `frontend/src/pages/reports/ReportsPage.jsx` + `components/{ReportTabs, DateRangeControl,
  CostByItemTable, ItemHeadcountTable, CumulativeCostView}.jsx` (new) — tablist of 3 distinct
  views, preset + `<input type="date">` range control, per-tab KPI `StatCard` row,
  loading/error/empty states via the shared `StateBlock`. Cost-by-item has an inline CSS % bar
  and a footer proving 100.00%; cumulative has a hand-drawn inline-SVG area chart (no charting
  dependency — Plan marks visualisations P2 and `AI_INSTRUCTIONS.md` §5 bars undiscussed deps).
- Shared files (additive): `frontend/src/App.jsx` — `/reports` moved into the existing
  `<ProtectedRoute requireManager>` group + import path; `frontend/src/navigation.js` —
  `minRankLevel: 2` on the Reports nav item. Deleted the `frontend/src/pages/Reports.jsx`
  placeholder.

**Developer verification performed this session:**
- `npm run build` — passed (Vite 8.2.2, 1679 modules, no errors).
- `npm test` — 5 files / 25 tests passed (15 prior + 10 new).
- Ran the generator + builders under Node and eyeballed output: top item ≈19.9% down to
  0.37%, percentages sum to exactly 100, headcount shows distinct requestors < request count
  (pens: 9 vs 16), cumulative monotonic and reconciles with the cost-by-item total.
- Dev server (`:5173` fresh + backend `:5263`) serving; `ReportsPage.jsx` transforms cleanly.
  **Not yet clicked through in a browser by the developer.**

**Assumptions made (ambiguous — flagged, not silently decided):**
- **No Reports wireframe exists** (`docs/Wireframe/` has 5 PNGs, none for Reports) and the
  Figma proto link needs edit access the tool does not have. UI built to the established design
  system (Dashboard/Inventory). Layout may differ from the Figma — needs a check by someone
  with access.
- Cumulative granularity assumed **monthly** (Plan doesn't specify).
- Date-range default assumed **last 90 days** (matches Plan §3.8 seed window).
- Currency stays `$` via `lib/format.js` — same unresolved VND `[ASK] #10` caveat as every
  page; not re-decided here.

**Left out of scope:** No backend (`IReportQueries`, controllers, SQL, migration) — that is
M3's work. No CSV export (Plan P2). No Recharts. No component/render tests for the new page
(helper logic is covered; the page itself is not). `AI_usage_report.md` not committed.

## 2026-08-28 — Reports page: fix tab-switch crash, add charts, add item search/filter

**Tool:** Claude Code (claude-sonnet-5).

**Task (developer-reported bugs + requests):** (1) white screen when opening the
Cumulative Cost tab and when switching Cost & Headcount → Cost by Item; (2) add a line
chart (monthly spend), a pie/donut (spend proportion by item) and a bar chart (most
requested items); (3) add a search box + Category and Approved-cost dropdown filters above
the report item list.

**Root cause of the white screen:** `useAsync` keeps the previous tab's payload in `data`
for one render after a tab switch, before the refetch resolves. The page then rendered a
cost-by-item table against cumulative data (no `rows`) or a headcount row against the
cost-by-item view (`percentOfTotal.toFixed` on `undefined`) → `TypeError` → blank screen.

**What AI produced / changed, by file:**
- `frontend/src/api/reports.js` — each payload now carries `kind` ('COST_BY_ITEM' |
  'HEADCOUNT' | 'CUMULATIVE').
- `frontend/src/pages/reports/ReportsPage.jsx` — `report = data && data.kind === tab ? data
  : null`; every read is gated on `report`, so a mid-switch render shows the loading state
  instead of throwing. Added filter state + toolbar wiring + a "no items match filters"
  empty state.
- `frontend/src/lib/reports.js` — `buildItemHeadcount` rows gain `unitsApproved` (Σ quantity).
- `frontend/src/pages/reports/reportFilters.js` (new) + `reportFilters.test.js` (new, 7
  cases) — pure search / category / cost-band filter over report rows.
- `frontend/src/pages/reports/components/ReportToolbar.jsx` (new) — shared `SearchInput` +
  Category `<select>` + Approved-cost band `<select>` + "N of M items" + Clear. Shown on the
  two item-list tabs only.
- `frontend/src/pages/reports/components/charts/{LineChart,BarChart,DonutChart}.jsx` (new) —
  inline SVG / HTML, no charting dependency (Plan marks visualisations P2; AI_INSTRUCTIONS
  §5 bars undiscussed deps). Single-series charts use one brand hue + native `<title>`
  tooltips; the donut uses a single-hue brand ramp by rank (darkest = largest), caps at 6
  slices + an "Other" roll-up, and labels identity in the legend text/value — not colour
  alone. Guidance taken from the `dataviz` skill (form-first, no dual-axis, no cycled hues,
  legend/table for identity).
- `CostByItemView.jsx` / `ItemHeadcountView.jsx` (new, replace the `*Table.jsx` files) —
  chart + table; footer switches to the shown subset when a filter is active, with a note
  that the `% of Total` column is still each item's share of the full period spend.
- `CumulativeCostView.jsx` — now two small-multiple line charts (monthly spend; cumulative)
  plus the table, instead of one hand-rolled area chart.

**Developer verification this session:**
- `npm run build` — passed (Vite, 1684 modules, no errors).
- `npm test` — 6 files / 34 tests passed (10 reports-lib + 7 reports-filters + 17 prior).
- Node run of the real data path: `kind` guard returns `null` for stale cross-tab data;
  donut input = 15 items → top 6 + Other; bar chart top units = Highlighters 138 / Sticky
  Notes 134 / A4 122; monthly line = 4 points; category + search filters return expected
  rows.
- Fresh dev server (`:5173`) serves every new module; backend `:5263` up. **The rendered
  page has not been clicked through in a browser by the developer this session.**

**Assumptions (flagged):**
- Donut interpreted as "share of approved spend by item"; bar chart as "most requested by
  units approved"; line chart as "approved spend per month" over whatever months the date
  range covers (a 12-month view is the `12mo` preset). None of these three metrics is
  spelled out in the Plan — confirm they are what was wanted.
- Filtering is client-side over the rows the report returned; when a filter is active the
  donut/bar reflect the filtered subset and the table footer shows the shown subtotal.
- Still no Reports wireframe / Figma access — chart and toolbar styling follow the existing
  design system.

**Left out of scope:** full crosshair-tooltip interaction layer on the charts (native SVG
`<title>` only); dark-mode chart steps (app has no dark mode); CSV export; any backend.
`AI_usage_report.md` not committed.

## 2026-08-30 — Reports page upgrade: meta bar, CSV/print, sortable columns, 2 new tabs, usage snapshot

**Tool:** Claude Code (claude-sonnet-5).

**Task:** Prescriptive spec (5 groups) to make `/reports` read as a financial report:
(1) a document-style metadata bar on every tab; (2) Export-CSV + Print buttons; (3) sortable
columns on the two item tables; (4) two new tabs — Inventory Valuation (point-in-time, reuses
the inventory API) and By Team (approved spend grouped by approving manager); (5) a "Top
Consumed Items" section on the Cumulative tab. Frontend only; no backend, no new npm packages.

**What AI produced / changed, by file** — full list in `docs/development/reports-page.md` §4.
New: `lib/csvExport.js`, `pages/reports/tableSort.js`,
`pages/reports/components/{SortHeader,ReportMetaBar,InventoryValuationView,TeamExpenditureView}.jsx`.
Modified: `api/mock/reports.mock.js` (added `MOCK_TEAM_MAP` export **only** — generator, PRNG
and seed untouched per the brief), `lib/reports.js` (`buildTeamExpenditure`; `buildCumulativeCost`
now also returns `topConsumed`), `api/reports.js` (`getTeamExpenditureReport` + contract block),
`ReportsPage.jsx` (2 tabs, inventory branch via a 2nd `useAsync`, Export/Print buttons, meta
bar, injected `@media print` stylesheet, `ITEM_TABS` set), `CostByItemView.jsx` /
`ItemHeadcountView.jsx` (sortable), `CumulativeCostView.jsx` (Top Consumed section),
`lib/reports.test.js` (+3 cases).

**Developer verification this session:**
- `npm run build` — pass (Vite, no errors/warnings).
- `npm test` — 37 passed (6 files); `npx vitest run reportFilters` — 8 passed, unchanged.
- Node harness on the real data path: By-Team 4 teams, shares sum to exactly 100.00, sorted
  desc; `topConsumed` = top 5 by units; inventory valuation total $5,727.95 / 7 in stock /
  1 reorder; CSV quoting of `Lever Arch Files, A4, Pack of 5` and embedded `"` verified.
- Dev server serves every new module; HMR clean. **Not clicked through in a browser by the
  developer; print output not visually inspected; no render tests for the new views.**

**Assumptions (flagged, not silently decided):**
- No Reports wireframe / Figma access — styling follows the existing design system.
- Inventory Valuation has **no Category column** — `MOCK_INVENTORY` carries no category and
  deriving one from the name would be fabricated; single "Item" column instead (documented in
  the component and CSV).
- Stock-status badge uses Tailwind `emerald/amber/red` (the brief's green/amber/red intent);
  the design-system `status` tokens are dark/grey/red. Only place in the app doing this.
- `MOCK_TEAM_MAP` team names are invented per the brief; stands in for the
  `Users.SuperiorEmployeeNumber` join. Unmapped requestors → "Unassigned".
- `BY_TEAM` KPI tiles (Approved Spend / Teams / Approved Requests) not in the brief — chosen
  to match the other date-range tabs. `cost-by-team` is not in the Plan §4.2 endpoint
  catalogue — flagged for the reviewer.
- CSV for the two item tabs exports the **filtered** rows (what's shown), in report order.

**Left out of scope:** no backend; no CSV for a "current sort order"; no dark-mode chart
steps; no component/render tests for the new views. Shared frontend files touched: none
outside `pages/reports/`, `lib/`, `api/` (route/nav were already wired in the prior session).
`package.json` unchanged; `package-lock.json` shows 30 unrelated deletions from an earlier
session. Not committed.
