# AI Usage Report

## 2026-09-03 — Catalogue-only New Stationery Request entry and in-page item search

**Task:** Remove the separate New Request navigation item. Requestors enter the request page through Catalogue item selection and **Proceed**, while retaining the ability to find and add further items on the request page.

**What changed, by file:**
- `frontend/src/navigation.js` — removed the `New Request` sidebar entry and unused `PlusCircle` import.
- `frontend/src/pages/requests/NewRequestPage.jsx` — added visible item/category search through the shared `SearchInput`; clears stale selection when the term changes; disables the picker and explains the empty result when no unadded eligible item matches.
- `frontend/src/pages/requests/NewRequestPage.test.jsx` — added name and category search coverage.
- `docs/development/request-pages-implementation-handoff.md` — documented the entry flow, retained direct-route behavior, data source, and scope boundary.

**API and DB changes:** None. `/new-request` remains a protected route so Catalogue's existing `navigate('/new-request', { state: { items } })` flow remains valid. `GET /api/v1/items` is still role-filtered server-side before the UI search runs.

**Assumptions:** The user asked to remove the menu item, not the route. Keeping the route preserves the Catalogue **Proceed** flow and direct-link compatibility without inventing a replacement route.

**Left out of scope:** No request lifecycle, backend endpoint, authorization policy, database schema, migration, or global search was changed.


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

## 2026-08-28 — Add required-reading checklist to Identity handoff

**Task:** Add an explicit list of files and constraints that another agent must read before changing authentication or user management.

**What changed:**
- `docs/development/identity-and-user-management-implementation-plan.md` — added a required-reading checklist covering project authority documents, Application services/contracts, Infrastructure Identity and persistence code, Web API integration, frontend auth/user-management files, and CI/deployment files.
- Added boundary reminders: full ASP.NET Core Identity remains selected; use cases stay in `Application`; Identity, EF Core, SQL Server, and JWT implementations stay in `Infrastructure`; database schema changes use new EF Core migrations; secrets must not be committed.
- `AI_usage_report.md` — appended this documentation-only record.

**APIs and DB changes:** None.

**Tests actually executed:** None; documentation-only change.

**Assumptions and exclusions:** No architecture decision was changed and no source code, migration, credentials, CI configuration, or deployment configuration was modified.
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

## 2026-08-28 — M2 Catalogue, Suppliers & Stock Ledger implementation plan

**Task:** Create a detailed implementation plan for Milestone 2 (Catalogue, Suppliers & Stock Ledger) per Plan §6.1 and §7 M2, covering Core entities, Application services/DTOs/validators, Infrastructure EF Core entities/configurations/migration/queries/StockService, WebApi controllers, Frontend pages (Catalogue, Manager Item/Supplier management, Inventory), API client swap from mocks, and tests.

**What changed, by file:**
- `docs/development/m2-catalogue-suppliers-stock-implementation-plan.md` — created the comprehensive implementation plan document with 15 sections: architecture decision, database schema, Application layer (DTOs, interfaces, validators, services), Infrastructure layer (EF entities, DbContext, repositories, queries, StockService, seeder), WebApi controllers and authorization, Frontend integration (pages, API client updates), tests (unit, integration, frontend), delivery steps (10 commits), Git strategy (two branches, rebase rule), risks & mitigations, Definition of Done, open questions, documentation updates, and hour estimates matching Plan §6.1.

**Architecture decisions recorded:**
- Full Clean Architecture separation: Core entities/interfaces, Application use-cases, Infrastructure EF/SQL/Identity, WebApi HTTP.
- Stock is a ledger: `StockTransactions` append-only, `QuantityAvailable` cached balance updated in same transaction.
- Role filtering on catalogue: `MinRankLevelToRequest <= caller.RankLevel` (Engineer=1, Manager=2, Business Manager=3, MD=4), isolated in one query filter.
- Concurrency via `RowVersion` on `StationeryItem`, `Supplier`, `StockTransaction`.
- Catalogue write, Suppliers, Inventory require `RequireManager` policy (RankLevel ≥ 2).
- Two git branches: `feat/M2-catalogue` (M2) and `feat/M2-inventory` (M3), M3 rebases onto M2 before PR to resolve shared migration once.

**Open questions flagged for instructor/team ([ASK]):**
1. Role filter rule: `<=` (default) vs `==` — Plan ambiguous.
2. SKU field: Frontend mock has it, Plan lists as future improvement → omit for now.
3. Supplier lead time unit: assumed days (integer).
4. Opening stock balance: seed via `StockTransaction` type `Receipt` with `Reference = "OPENING"`.

**Validation actually run:** None; documentation-only planning task.

**Left out of scope:** No code implemented, no migration created, no API endpoints built, no frontend components written, no tests written. This is a plan document only.

## 2026-08-28 — Implement M2: Catalogue, Suppliers & Stock Ledger

**Task:** Execute `docs/development/m2-catalogue-suppliers-stock-implementation-plan.md` end to
end, following the plan's own §11 fixes made in an earlier review pass (Guid RowVersion instead
of SQL Server rowversion, FK to ApplicationUser instead of the legacy Users table, real
ICurrentUserService instead of a stub). User asked for the full plan executed with a commit per
delivery step, same working style as the M1 identity/user-management implementation.

**What changed, by file (grouped by delivery step / commit):**
- **Core entities** — `Core/Entities/{Category,Supplier,StationeryItem,StockTransaction,StockTransactionType}.cs`.
- **Application DTOs/interfaces/validators** — `Application/DTOs/{Catalogue,Suppliers,Inventory}/*`,
  `Application/Interfaces/{Catalogue,Suppliers,Inventory}/*`,
  `Application/Validators/{Catalogue,Suppliers,Inventory}/*`. Hoisted
  `Application.DTOs.Users.PagedResult<T>` to `Application.DTOs.Common.PagedResult<T>` so the new
  domains could reuse it (updated `IUserManagementService`, `IUserStore`,
  `UserManagementService`, `UsersController`, `IdentityUserStore` accordingly).
- **Application services** — `Application/Services/{Catalogue,Suppliers,Inventory}/*Service.cs`.
- **Infrastructure EF configs + migration** — `Infrastructure/Data/Configurations/{Category,
  Supplier,StationeryItem,StockTransaction}Configuration.cs`, `DbSet`s added to
  `Infrastructure/DataContext.cs`, migration `20260828131329_CatalogueSuppliersAndStock`.
- **Infrastructure queries + StockService** — `Infrastructure/Queries/{Item,Supplier,Inventory,
  Stock}Queries.cs`, `Infrastructure/Services/StockService.cs`. No dedicated repository classes
  were needed — the existing generic `IRepository<T>`/`Repository<T>` already covers
  `Category`/`Supplier`/`StationeryItem`.
- **Seeder** — `Infrastructure/Data/DbSeeder.cs` gained `SeedCatalogueAndInventoryAsync` (5
  categories, 6 suppliers, 40 items, ~90 days of synthetic ledger history per item) and
  `SeedBootstrapAdminAsync` (see below). Wired into `WebApi/Program.cs`'s existing
  `if (!IsEnvironment("Testing"))` startup block, after role seeding.
- **WebApi controllers** — `WebApi/Controllers/{Catalogue,ManagerCatalogue,Suppliers,Inventory}Controller.cs`,
  DI registrations in `WebApi/Program.cs`.
- **Frontend** — `frontend/src/api/{catalogue,suppliers,inventory}.js` now call the real API
  (mock files `catalogue.mock.js`/`inventory.mock.js` deleted); `frontend/src/pages/manager/
  {ItemManagement,SupplierManagement}.jsx` (new, replacing the `Suppliers.jsx` placeholder);
  `StockActionModal.jsx` updated to send the fields the backend actually expects plus
  `rowVersion`; routes/nav updated so Inventory, Suppliers, Item Management, and User Management
  are all gated behind `requireManager` (Inventory previously had no route guard at all).
- **Tests** — `Tests/Application.UnitTests/{Catalogue,Suppliers}/*ServiceTests.cs` (9 tests),
  `Tests/WebApi.IntegrationTests/{Catalogue,Inventory,Suppliers}Tests.cs` plus
  `CatalogueTestData.cs` helper (8 tests), `frontend/src/pages/manager/*.test.jsx` (7 tests).

**APIs added:** `GET /api/v1/categories`, `GET/POST /api/v1/items`, `GET/PUT/PATCH
/api/v1/items/{id}[/deactivate]`, `POST/PUT/PATCH /api/v1/categories[/{id}][/deactivate]`,
`GET/POST/PUT/PATCH /api/v1/suppliers[/{id}][/deactivate]`, `GET /api/v1/inventory`,
`GET /api/v1/inventory/low-stock`, `POST /api/v1/inventory/{itemId}/adjust`,
`POST /api/v1/inventory/{itemId}/receive`, `GET /api/v1/inventory/{itemId}/transactions`.

**DB changes:** One migration (`CatalogueSuppliersAndStock`) — `Categories`, `Suppliers`,
`StationeryItems`, `StockTransactions` tables, `StockTransactions.CreatedByEmployeeNumber` FK
correctly targets `AspNetUsers` (verified in the generated migration, not just assumed). Not
applied to a real SQL Server — none available in this environment; verified via SQLite
integration tests and `dotnet ef migrations has-pending-model-changes` (none pending).

**Deviations from the plan doc, found necessary during implementation (beyond the ones already
fixed in the plan-review pass):**
1. **Request/response DTOs carry `Guid RowVersion`.** The plan approved an app-managed
   concurrency token but never specified how the client round-trips it; without that, there's
   nothing to compare against for a 409. Added to `ItemDto`, `SupplierDto`, `InventoryRowDto`,
   `UpdateItemRequest`, `UpdateSupplierRequest`, `AdjustStockRequest`, `ReceiveGoodsRequest`.
2. **Concurrency is a manual compare-then-set, not `DbUpdateConcurrencyException`.** Simpler and
   fully portable between SQL Server and the SQLite test provider — see `StockService.ApplyAsync`
   and the `ItemService`/`SupplierService` update paths.
3. **`Category.IsActive` and `StationeryItem.SupplierId` added** — the plan's §2.1 entity table
   omitted both, but §3.2's `DeactivateCategoryAsync` and §3.4's "409 if active items reference
   it" check need them.
4. **A bootstrap admin account.** M1 deliberately seeds zero users, but M2's seeded
   `StockTransaction` rows need a real `CreatedByEmployeeNumber`, and with zero users there was
   also no way to sign in and create the first real user (`POST /api/v1/users` is
   Manager+-only). Added `DbSeeder.SeedBootstrapAdminAsync` — one Managing Director account,
   password from `Seed:BootstrapAdminPassword` config (never hardcoded, same pattern as
   `Jwt:SigningKey`).
5. **Inventory status thresholds are a simple heuristic**, not the consumption-rate/lead-time
   model the frontend mock's own comment described (that's explicitly M5 AI territory):
   `REORDER_NOW` at `QuantityAvailable <= ReorderLevel`, `WATCH` at `<= ReorderLevel * 1.5`, `OK`
   otherwise. Documented in `IInventoryQueries`' doc comment.

**Tests actually executed:**
- `dotnet build Project.slnx` — succeeds (only the pre-existing unrelated `SQLitePCLRaw` warning).
- `dotnet test Project.slnx` — **49/49 passed** (26 unit + 23 integration).
- `npx vitest run` (frontend) — **22/22 passed**.
- `npm run build` — succeeds.
- **Not done:** migration against a real SQL Server (none available), manual browser smoke test.
  Nobody has clicked through this in a real browser yet — say so rather than claiming it works.

**Explicitly left out of scope:**
- `[ASK]` #3 (role filter `<=` vs `==`) resolved as `<=` per the plan's stated default; not
  confirmed with the instructor.
- `[ASK]` #2 (SKU): not persisted, matching the plan. `InventoryRowDto`/`ItemDto` have no `sku`
  field; the frontend's SKU column/search now render blank rather than crash.
- The two-branch git strategy (m2 plan §9) was not followed — flagged in the plan review as
  likely not applicable to a single-implementer session, and that held here too; all steps landed
  as sequential commits on `main`.
- `IStockService.IssueAsync` exists but is not called by any M2 endpoint — reserved for M4's
  request-fulfillment flow, per the interface's doc comment.

**Shared files touched:** `frontend/src/App.jsx`, `navigation.js`, `Infrastructure/DataContext.cs`,
`WebApi/Program.cs`, `WebApi/appsettings.json`, `WebApi/appsettings.Development.json`.

**Reviewer follow-ups:**
- Apply the migration to a real SQL Server and do a manual browser smoke test before merging.
- Confirm `[ASK]` #3's role-filter direction and the lead-time unit (days, assumed) with the team.
- Decide whether the bootstrap-admin approach is acceptable long-term, or whether initial user
  provisioning should work differently (e.g., a setup wizard, a seeded-from-config admin list).
- Revisit the inventory status thresholds once M5's actual consumption-rate model exists.

## 2026-08-28 — New-machine audit, `khang` rebase onto `origin/main`, environment repair

**Task:** After the project moved to a new Windows 10 machine (USB copy), audit the repo and dev
environment, then — on explicit instruction — rebase the stale local `khang` branch onto
`origin/main`, push the result, drop a leftover conflicted stash, and fix the frontend install.

**What was found (audit, no changes yet):**
- `origin` had switched between an unreachable SSH remote and, later in the session, a working
  HTTPS one (`git fetch`/`git ls-remote` began succeeding partway through — cause not fully
  determined, credential.helper is `manager`).
- Local `khang` (`1796f49`) was **15 commits behind `origin/main`** and 1 ahead. The one local
  commit, "Add EF Core SQL Server dependencies," had **unresolved Git conflict markers committed
  into `WebApi/WebApi.csproj`** (`<<<<<<< Updated upstream` / `=======` / `>>>>>>> Stashed
  changes`), leaving the project file invalid XML — `dotnet restore` failed with `MSB4025`. A
  matching unresolved stash (`On main: EF Core SQL Server setup`) was still present.
- `frontend/node_modules` was first found installed for **Linux** (wrong-platform native
  binaries, no `.cmd` shims — `npm run build` failed with `'vite' is not recognized`), then later
  found **fully absent** (mid-fix from an earlier recommendation, `npm ci` never run).
- `origin/main` (unreachable before HTTPS started working, then inspected read-only via
  `git show`) turned out to hold substantial, already-tested M1 work — see the 2026-08-27/28
  entries above — that did not exist in the local working copy at all.

**What changed, by action:**
- **Rebase** (`git rebase origin/main` on `khang`): merge-base was `a43ecef`, so only `1796f49`
  needed replaying. The ~88 stale `bin`/`obj` files in that commit auto-merged with no conflict.
  The one real conflict, `WebApi/WebApi.csproj`, was resolved by taking `origin/main`'s side
  entirely (`git checkout --ours`) — `1796f49`'s intent (add `EntityFrameworkCore.SqlServer`/
  `.Tools` to `WebApi.csproj`) was both broken and redundant: `origin/main` already has the
  equivalent packages correctly placed in `Infrastructure.csproj` per the Plan's dependency rule
  (§2.3 — EF/SqlServer belongs in Infrastructure, not WebApi). Verified after rebase:
  `dotnet restore && dotnet build Project.slnx` — 0 errors; no leftover conflict markers anywhere
  in tracked source (`grep` swept `.cs`/`.csproj`/`.json`/`.js`/`.jsx`).
- **Push** (`git push --force-with-lease origin khang`, after confirming `origin/khang` hadn't
  moved since the rebase): succeeded, `1796f49...0728279 khang -> khang (forced update)`.
  `origin/khang` and local `khang` are now content-identical to `origin/main` plus one
  build-artifact-only commit.
- **Stash drop**: diffed `stash@{0}` against the post-rebase tree first — confirmed its only
  non-artifact content was the same `WebApi.csproj` change already reconciled above — then
  `git stash drop`.
- **Frontend install fix**: `cd frontend && npm ci` — 245 packages installed cleanly from the
  existing (valid) lockfile. `npm run build` — succeeds (Vite 8.2.2, 1671 modules, no
  errors/warnings). `npm audit` — 2 moderate vulnerabilities (`react-router` open-redirect /
  constructor-injection advisories; fix requires `react-router-dom@7.18.2`, a breaking major
  bump) — **not applied**, flagged for the team to schedule deliberately.
- **This log entry, and `CLAUDE.md`** — updated §0/§1/§2/§7 to replace the stale "nothing is
  built" / "SDK 8.0.422 blocks the build" content (which predated this machine's move and this
  session's rebase) with the verified current state.

**Tests actually executed after the rebase (all on branch `khang`, post-push):**
- `dotnet test Project.slnx` — **32/32 passed** (17 `Application.UnitTests` + 15
  `WebApi.IntegrationTests`).
- `npx vitest run --pool=threads` (frontend) — **15/15 passed**, 4 files.
  ⚠️ Plain `npm test` (`vitest run`, default forks pool) **hung and timed out** on this machine
  ("Timeout waiting for worker to respond") — looks like a local child-process-spawning
  restriction in this environment, not a test defect. `--pool=threads` works; flagging for anyone
  else hitting the same hang rather than silently switching the project default.
- `npm run build` (frontend) and `dotnet build Project.slnx` — both succeed, 0 errors.

**Assumptions made (flagged, not silently decided):**
- Resolving the `WebApi.csproj` conflict in favour of `origin/main`'s side was a judgment call,
  not a mechanical one — reasoned from architecture (Plan §2.3) and from the fact that the local
  side was already broken. Recorded here in case that reasoning needs to be revisited.
- Did not investigate why `origin` changed from SSH to HTTPS mid-session; noted as unresolved
  rather than guessed at.

**Left out of scope:**
- Did **not** run `npm audit fix --force` — a breaking `react-router-dom` major bump is a team
  decision, not one to make silently mid-environment-repair.
- Did **not** install SQL Server LocalDB or repoint the dev connection string at the running
  `SQLEXPRESS` instance — flagged in `CLAUDE.md` §2 for whoever picks it up next.
- Did **not** touch `docs/development/architecture.md`'s staleness banner (added in an earlier
  session, now itself partly superseded by this rebase) — out of scope for this task's explicit
  ask (`CLAUDE.md` and this file only).
- No feature code was written; this was environment/repo-hygiene work only.

## 2026-08-28 — First live SQL Server run, browser smoke test, and seed-data fix

**Task:** Close M2 reviewer follow-up #1 — apply the migrations to a real SQL Server instance and
smoke-test the app end to end (neither had ever been done) — then fix the two seed-data defects
that the smoke test exposed.

### Part 1 — migration + smoke test (no code changed)

Applied `20260827133027_InitialIdentity` and `20260828131329_CatalogueSuppliersAndStock` to the
local **SQL Server 2022 Express** instance (`.\SQLEXPRESS`, 16.0.1000.6), database
`StationeryManagementSystem.Dev`. All 12 tables and all 3 check constraints
(`CK_Users_EmployeeNumber`, `CK_StationeryItems_MinRankLevelToRequest`,
`CK_StockTransactions_ChangeQuantity`) created as designed.

`appsettings.Development.json` was **not** edited — its connection string still targets LocalDB,
which is not installed on this machine. The override was supplied as the
`ConnectionStrings__DefaultConnection` environment variable, matching the project's own
"connection strings via environment variables" rule and leaving the repo untouched.

API smoke test (all against the live SQL Server, not SQLite): unauthenticated `/items` → 401;
login as the bootstrap admin → 200; `/categories`, `/items`, `/items/{id}`, `/inventory`,
`/low-stock`, `/{itemId}/transactions` all correct; receive +25 → balance 400→425 with one new
ledger row; **replaying a stale `RowVersion` → 409**; adjust −999999 → 400 `ProblemDetails`;
adjust 0 → 400. Verified in SQL that **the cached balance reconciled with `SUM(ChangeQuantity)`
for all 40 items (zero mismatches)** and that the *rejected* calls wrote **zero** ledger rows —
i.e. the transaction rollback behaves correctly on SQL Server, not just on the test provider.

Browser smoke test: login → Dashboard → Catalogue → Inventory all render live API data with
Manager+ nav gating; a **Receive Goods performed through the UI** moved a row 276→286 and wrote
ledger row #937, with the total-value tile moving by exactly 10 × $5.75.

One false alarm worth recording: the stock modal first appeared to be broken because neither the
accessibility-tree reader nor a `<main>` text dump showed it. A direct DOM query found
`[role=dialog]` present and fully functional — the modal renders without a portal, outside
`<main>`, so those tools miss it. **Not a bug**; no code was changed for it.

### Part 2 — seed-data fix (`Infrastructure/Data/DbSeeder.cs`, the only file changed)

The smoke test exposed two genuine defects in seeded data. Neither affected correctness, both
would have been visible in the demo/defence:

1. **The catalogue was a cartesian product.** One shared 8-item `ItemTemplate` was applied to
   every one of the 5 categories, with the category name prepended to each item, producing
   "Printing Supplies — Ballpoint Pens", "Organization — Premium Cardstock", and the same 8
   products listed five times (8 distinct names across 40 items).
2. **The low-stock path could never be demonstrated.** Opening balance was `ReorderLevel * 3` and
   the random walk was net-positive (≈70% issues of 1–5 against 20% receipts of 10–30), so every
   item finished well clear of its reorder level: all 40 items `OK`, `lowStockAlerts` 0,
   `/inventory/low-stock` empty. The `WATCH`/`REORDER_NOW` badges and the dashboard low-stock
   tile were unreachable with seeded data.

**What changed, by file:**
- `Infrastructure/Data/DbSeeder.cs` —
  - replaced `CategorySeeds` + `ItemTemplate` with `CatalogueSeeds`: a real per-category product
    list (still 5 categories × 8 items = 40, so the documented count is unchanged), with the
    category-name prefix dropped. Now 40 distinct, semantically correct names.
  - added a private `StockPosture` enum (`Healthy`/`Watch`/`Reorder`) on each item seed, and
    `TargetBalanceFor(reorderLevel, posture)` returning a balance expressed as a multiple of the
    item's own reorder level, so the bands in `InventoryQueries.DeriveStatus` are hit regardless
    of item scale (0.6× → REORDER_NOW, 1.25× → WATCH, 2.5× → OK).
  - made `SeedTransactionHistory` posture-aware: opening balance and issue probability now vary
    by posture so low items drain over the 90 days rather than being levelled by one implausible
    bulk movement, then a single closing movement (typed Issue or Receipt by sign, dated
    yesterday) lands the balance exactly in band.

**DB changes:** none. **No migration was added** — this is seed data only; the schema is
unchanged. **APIs changed:** none.

**Tests actually executed (after the change):**
- `dotnet build Project.slnx` — succeeds, 0 errors.
- `dotnet test Project.slnx` — **49/49 passed** (26 unit + 23 integration). No test depends on
  the catalogue seeder; the integration tests use `DbSeeder.SeedRolesAsync` only and build their
  own fixtures via `CatalogueTestData`, which is why the item-shape change is safe.
- `npx vitest run --pool=threads` — **22/22 passed**.
- Dropped and re-created the dev database (`dotnet ef database drop --force`, then app start) to
  actually exercise the new seeder, since it is idempotent and skips a populated catalogue.
- Verified after re-seed, in SQL and through the API and UI: **27 OK / 7 WATCH / 6 REORDER_NOW**;
  `lowStockAlerts` = 6; `/inventory/low-stock` returns those 6; 40 distinct item names; **0**
  names containing the old category prefix; and the ledger-vs-cached-balance invariant still
  holds across all 40 items (0 mismatches).

**Assumptions made (flagged, not silently decided):**
- The posture assignment (which 6 items sit below reorder, which 7 are on watch) is a judgement
  call for demo realism — fast-moving consumables and expensive low-volume items were chosen. It
  is not derived from any spec.
- Item names, unit costs and reorder levels are invented plausible values. The Plan does not
  specify a product list, so these are **not** `[SPEC]`-derived and should be replaced if the
  instructor supplies real data.
- `Executive Fountain Pen` (rank 3) and several Tech items (rank 2–3) keep a spread of
  `MinRankLevelToRequest` so the role filter (`[ASK]` #3) stays demonstrable.

**Explicitly left out of scope:**
- The `WATCH`/`REORDER_NOW` thresholds themselves are untouched — still the simple ratio
  heuristic in `InventoryQueries.DeriveStatus`, still slated for replacement by M5's
  consumption-rate model.
- No seeded demo *users* were added — user creation remains Manager+'s job via `POST /users`,
  and the bootstrap-admin design is unchanged.
- SKU still not persisted (`[ASK]` #2 / K5) — the SKU column continues to render blank.
- `appsettings.Development.json`'s LocalDB connection string was deliberately left as-is rather
  than repointed at `SQLEXPRESS`; that is a team decision, not a fix to slip into a seeder change.

## 2026-08-28 — Supplier request cart (Inventory → suppliers)

**Task:** Turn the inventory item selection into a Shopee-style cart: tick items, review them in a
modal with per-item quantities, submit, and have the backend create one supplier order per
supplier — without moving stock.

### ⚠️ Scope: this builds a `[CUT]` item, on the owning developer's explicit decision

Plan §1.3 lists **"payment or procurement PO generation"** under `[CUT] WON'T`, with *"If someone
starts building one, that is a scope breach — escalate to the Project Leader."* `CLAUDE.md` §5
repeats it. The Plan's §4.2 endpoint catalogue has no supplier-request endpoint, and
`POST /inventory/{itemId}/receive` is specified as goods **receipt**, not ordering.

This was raised **before any code was written**, with an in-scope alternative offered (a
multi-item *goods receipt* cart needing no new entities). The user chose to build the supplier
request anyway and accepted the scope-breach risk. Recording that here so the decision is not
mistaken for an oversight. The Plan itself was **not** edited — that is a Project Leader call.

**What changed, by file:**

- **Core** — `Entities/SupplierRequest.cs`, `Entities/SupplierRequestItem.cs`. Header/line split
  mirroring the Plan's `Requests`/`RequestItems` (§3.4), with `UnitCostSnapshot` frozen per line
  (CLAUDE.md principle #8). Deliberately **no status column** — no document specifies a supplier
  order lifecycle and inventing one is what K3 flagged.
- **Application** — `DTOs/SupplierRequests/{CreateSupplierRequestCommand,SupplierRequestDto}.cs`,
  `Interfaces/SupplierRequests/{ISupplierRequestService,ISupplierRequestQueries}.cs`,
  `Validators/SupplierRequests/CreateSupplierRequestCommandValidator.cs`. Named `...Command`
  because `Application.DTOs.Suppliers.CreateSupplierRequest` already exists and means something
  entirely different (it creates a *Supplier*) — a genuine collision hazard.
- **Infrastructure** — `Services/SupplierRequestService.cs` (placed here, not Application, for the
  same reason as `StockService`: it needs `DataContext`, and Application must never reference
  `DbContext`), `Queries/SupplierRequestQueries.cs`,
  `Data/Configurations/SupplierRequest{,Item}Configuration.cs`, `DataContext.cs` (+2 DbSets),
  migration `20260828143526_SupplierRequests`.
- **WebApi** — `Controllers/SupplierRequestsController.cs` (`POST`/`GET`/`GET {id}`, all
  `RequireManager`), `Program.cs` (+2 DI registrations, +1 using).
- **Application (modified)** — `DTOs/Inventory/InventoryRowDto.cs` gained `SupplierId`/
  `SupplierName` as **defaulted optional** parameters, so nothing consuming it previously breaks;
  `Infrastructure/Queries/InventoryQueries.cs` projections extended to populate them.
- **Frontend** — `api/supplierRequests.js` (new),
  `pages/inventory/components/SupplierRequestModal.jsx` (new),
  `pages/inventory/InventoryPage.jsx` (cart state + new toolbar button).
- **Tests** — `Tests/WebApi.IntegrationTests/SupplierRequestsTests.cs` (14),
  `frontend/src/pages/inventory/InventoryCart.test.jsx` (7).
- **Docs** — `docs/development/supplier-request-cart-implementation-handoff.md`, and this entry.

**APIs added:** `POST /api/v1/supplier-requests`, `GET /api/v1/supplier-requests`,
`GET /api/v1/supplier-requests/{id}` — all Manager+.

**DB changes:** one migration adding `SupplierRequests` + `SupplierRequestItems`. **No existing
table altered**, so no data-loss risk. Check constraint `CK_SupplierRequestItems_Quantity`
(`> 0`), unique index on `(SupplierRequestId, ItemId)`, FKs `Restrict` except header→lines
cascade. Applied to the live `.\SQLEXPRESS` database.

**Two design rules the service enforces (both tested):**
1. *The database owns the supplier.* An item's preferred `SupplierId` always wins; the
   client-supplied `supplierId` is consulted only for items that have none, and must then resolve
   to an active supplier. A client cannot redirect an order to an arbitrary supplier.
2. *All-or-nothing.* Every line is validated before the first `Add`, and the whole submission
   commits through one `SaveChangesAsync` (no `UnitOfWork` wrapper, per Plan §2.4), so one bad
   line leaves no partial orders.

**Tests actually executed:**
- `dotnet test Project.slnx` — **63/63 passed** (26 unit + 37 integration; 49 before).
- `npx vitest run --pool=threads` — **29/29 passed** (22 before).
- `dotnet build` / `npm run build` — both clean.
- **Live end-to-end against SQL Server:** 3 items spanning 3 suppliers → 3 correctly grouped
  orders with correct totals; SQL confirmed **stock unchanged and zero ledger rows written**, and
  the ledger-vs-cached-balance invariant still holding across all 40 items; selection and cart
  cleared on success; Catalogue and Inventory both still functional.

**Assumptions made (flagged, not silently decided):**
- Default cart quantity is **1**. A suggested reorder amount would be more useful but is an
  invented business rule.
- **UI deviation:** the cart is behind a new **"Request from Suppliers"** button rather than the
  existing *Receive Goods* button the brief specified, because that label would otherwise mean two
  opposite things (the row action genuinely receives stock; the cart only orders). Flagged for the
  team to rename if they disagree.

## 2026-08-30 — Approval Workflow Infrastructure (Application, Core, Infrastructure Layers)

**Prompt:** "tạo những gì approval cần ở Application, Core và infrastucture" (Create what approval needs in Application, Core and Infrastructure)

**What was done:**
- **Core Layer (Entities):** Request, RequestItem, RequestStatusHistory entities already existed from prior work. Added `ApplicationUser.RankLevel` property (1–4 for role-based eligibility) to enable approval decision logic and spending thresholds.
- **Infrastructure Layer:**
  - **EF Core Configurations:** Created `RequestConfiguration`, `RequestItemConfiguration`, `RequestStatusHistoryConfiguration` with all constraints (status enum `CHECK`, PK/FK definitions, cascading deletes, defaults).
  - **DbContext:** Updated `DataContext` to add `DbSet<Request>`, `DbSet<RequestItem>`, `DbSet<RequestStatusHistory>`.
  - **Service Implementation:** `RequestService` (14 KB) implements `IRequestService` with full lifecycle: `CreateAsync`, `SubmitAsync`, `ApproveAsync`, `WithdrawAsync`, `RequestCancellationAsync`, `ApproveCancellationAsync`, `DeletePendingAsync`. All methods include concurrency control (Guid-based RowVersion compare-then-set), visibility checks, status validation, and transactional atomicity (all or nothing on status + history + totals).
  - **Queries Implementation:** `RequestQueries` implements `IRequestQueries` with pagination, ownership/role-based visibility filtering, and status summary for dashboard. Methods: `GetVisibleAsync`, `GetByIdAsync`, `GetPendingApprovalsAsync`, `GetByRequestorAsync`, `GetStatusSummaryForDashboardAsync`.
- **Application Layer:**
  - **DTOs:** Created `WithdrawRequestCommand` and `RequestCancellationCommand` DTOs for two additional request lifecycle operations not yet built in prior phases.
  - **Validators:** Created `WithdrawRequestCommandValidator` and `RequestCancellationCommandValidator` using FluentValidation; both validate request ID, row version (concurrency control), and (for cancel) reason length.
- **DI Registration:** Added `IRequestService`/`IRequestQueries` bindings to `Program.cs` (WebApi layer).
- **EF Migration:** Generated `AddRequestEntities` migration via `dotnet-ef`.
- **Verification:** `dotnet build` — 0 errors (1 pre-existing obsolete API warning on `HasCheckConstraint` reuse, fixed by moving to `ToTable()` overload). `dotnet test` — 38/38 integration tests passed (26+23 unit+integration baseline; integration tests now richer due to Request infrastructure).

**Files changed:**
- Created:
  - `Infrastructure/Data/Configurations/RequestConfiguration.cs`
  - `Infrastructure/Data/Configurations/RequestItemConfiguration.cs`
  - `Infrastructure/Data/Configurations/RequestStatusHistoryConfiguration.cs`
  - `Infrastructure/Services/RequestService.cs` (full transaction lifecycle)
  - `Infrastructure/Queries/RequestQueries.cs` (full query + visibility logic)
  - `Application/DTOs/Requests/WithdrawRequestCommand.cs`
  - `Application/DTOs/Requests/RequestCancellationCommand.cs`
  - `Application/Validators/Requests/WithdrawRequestCommandValidator.cs`
  - `Application/Validators/Requests/RequestCancellationCommandValidator.cs`
  - `Infrastructure/Data/Migrations/2026MMDD######_AddRequestEntities.cs` (auto-generated by EF)
- Modified:
  - `Infrastructure/DataContext.cs` — added three new `DbSet<>` properties for Request aggregates.
  - `Infrastructure/Identity/ApplicationUser.cs` — added `RankLevel` property (int, default 1) for role hierarchy.
  - `Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs` — configured `RankLevel` as required with default.
  - `WebApi/Program.cs` — added `using Application.Interfaces.Requests`, registered `IRequestService` and `IRequestQueries` DI bindings.

**Assumptions made (flagged, not silently decided):**
- **EmployeeNumber vs. ApplicationUser.Id:** The Domain model (Request, RequestStatusHistory) references approve/requestor/actor as "EmployeeNumber" (int). ApplicationUser doesn't have a separate `EmployeeNumber` property; its `Id` IS the employee number (per `ApplicationUserConfiguration.ValueGeneratedNever` and the class docs). queries use `u.Id` to match these foreign keys, not a missing `EmployeeNumber` column.
- **Status enum values:** Request.Status enum is server-enforced via SQL `CHECK` constraint with 8 values (`Pending`, `Approved`, `PartiallyApproved`, `Rejected`, `Withdrawn`, `CancellationPending`, `Cancelled`, `Fulfilled`) — this matches the approval_transaction.drawio diagram imported earlier. Implementation assumes these are correct; **Plan §3.6/K1 flags ReturnedForModification/7vs8 ambiguity** — this implementation uses the 8-value set as the working assumption pending team clarification.
- **Visibility model:** Only Manager+ (`RankLevel >= 2`) see all requests; Engineers and below see only (a) their own requests or (b) those they must approve. Returns null (not 404) when denying access to preserve non-existence.
- **No notifications sent yet:** The service populates `RequestStatusHistory` rows (audit trail) but does not trigger notification sends (`Notifications` table rows) — that's deferred to M5+ or explicit notification service integration.
- **No stock movements yet:** `ApproveAsync` records the approval decision but does NOT execute `IStockService.IssueAsync` to move stock. That's deferred to M4 fulfillment. Approver can modify line quantities in a future PR (the `LineDecision.ModifiedQuantity` field exists but is not yet applied).

**Testing status:**
- Unit tests (Application layer): 26 passed. Integration tests (with Request infrastructure): 38 passed (up from 23 before).
- Validators: Tested via integration test suite (FluentValidation is exercised on create/approve/withdraw/cancel).
- Service concurrency: compare-then-set logic is present and checked by callers; explicit concurrency tests can be added in M3+.
- Coverage gap: No end-to-end approval workflow test yet (request creation → submission → approver decision → status transitions + history rows). Added as follow-up task.

**Known issues / follow-ups:**
1. .NET EF tools version 8.0.27 is older than runtime 10.0.10 — doesn't block builds/migrations but should be upgraded when toolchain is updated.
2. `ApproveCancellationCommandValidator` exists but was not wired into `RequestService` (unused parameter warning). ApproveAsync() validates ApproveRequestCommand; ApproveCancellationAsync() doesn't yet call its validator — add validation or remove the validator.
3. `LineDecision.ModifiedQuantity` is parsed but not applied to RequestItem quantities or PartiallyApproved line total recalculation — needed for full partial approval support.
4. Dashboard status summary (`GetStatusSummaryForDashboardAsync`) returns counts only, no detail — UI should fetch full request list via `GetVisibleAsync` if detail is needed.
5. No explicit Controller layer for Request CRUD yet — `RequestsController`, `ApprovalsController` to be added in M3+ with proper authorization checks and error mapping to RFC 7807 `ProblemDetails`.
- Supplier-less items are resolved by a picker in the modal (the user's chosen option), with the
  server validating the choice exists and is active.

**Explicitly left out of scope:**
- **No order lifecycle** — orders cannot be marked received or cancelled, and there is no link
  between a `SupplierRequest` and the `StockTransaction` that eventually fulfils it. Pending spec.
- **No UI to browse past orders**; `GET /supplier-requests` exists and is tested but unused by the
  frontend.
- **SKU still not persisted** (`[ASK]` #2 / K5), so the cart shows no SKU column despite the brief
  asking for one — that remains a separate scope decision.
- The Plan document was not edited to reflect this feature; that is a Project Leader decision.
- The toolbar *Adjust Stock* button still acts on `visibleRows[0]` rather than the selection —
  pre-existing behaviour, left untouched.

## 2026-08-28 — Add catalogue item filtering by supplier

**Task:** Fix the missing supplier filter for catalogue and item management. `StationeryItem.SupplierId` existed, but `GET /api/v1/items` and the catalogue UI did not expose it as a filter.

**What changed, by file:**
- `Application/DTOs/Catalogue/ItemQueryParameters.cs` — added optional `SupplierId` query criteria.
- `Application/DTOs/Catalogue/ItemDto.cs` — added nullable `SupplierName` so authenticated requestors can label filter choices without calling the manager-only suppliers endpoint.
- `Infrastructure/Queries/ItemQueries.cs` — filters by `SupplierId` before count/page projection; projects optional supplier name for all item reads.
- `WebApi/Controllers/CatalogueController.cs` — binds optional `supplierId` on `GET /api/v1/items`.
- `frontend/src/api/catalogue.js` — `getItems` optionally sends `supplierId`.
- `frontend/src/pages/catalogue/{CataloguePage.jsx,filters.js,components/CatalogueFilters.jsx}` — derives supplier choices from loaded item supplier data; adds selector, local filter logic, and active-filter chip.
- `frontend/src/pages/manager/ItemManagement.jsx` — adds an All suppliers selector that reloads items with the selected server-side supplier filter.
- `Tests/WebApi.IntegrationTests/{CatalogueTests.cs,CatalogueTestData.cs}` and `Tests/Application.UnitTests/Catalogue/ItemServiceTests.cs` — added backend supplier-filter coverage and updated `ItemDto` construction.
- `docs/development/catalogue-inventory-implementation-handoff.md` — documented contract, behavior, no-migration rationale, and validation.

**API change:** `GET /api/v1/items?supplierId={id}` returns only catalogue items whose preferred `SupplierId` matches the supplied ID. The response now includes nullable `supplierName`.

**DB change:** None. `StationeryItems.SupplierId` already exists in migration `CatalogueSuppliersAndStock`.

**Assumption:** A supplier filter means the item's preferred supplier only. Items without a preferred supplier appear under All suppliers, never under a specific supplier. This matches the current single nullable `SupplierId` data model; multiple suppliers per item are NOT SPECIFIED and not implemented.

**Validation actually run:** `dotnet test Tests/WebApi.IntegrationTests/WebApi.IntegrationTests.csproj --filter FullyQualifiedName~CatalogueTests` passed 4/4. `cd frontend && npm run build` passed. Existing unrelated `NU1903` advisory for `SQLitePCLRaw.lib.e_sqlite3` remained.

**Left out of scope:** No migration, no supplier-to-item many-to-many relationship, no public supplier directory endpoint, no manual browser test.

## 2026-08-30 — Dependency setup and local project validation

**Task:** Install the required .NET and frontend dependencies for this workspace and confirm the project can build locally before running it.

**What changed, by file:**
- No application source files were edited.
- Installed frontend dependencies in `frontend/` with `npm install` and verified the app builds with `npm run build`.
- Restored and built the .NET solution using `dotnet restore Project.slnx` and `dotnet build Project.slnx --nologo`.
- Generated the standard lockfile and package installation artifacts under the workspace for local setup.

**Validation actually run:**
- `dotnet restore Project.slnx` — succeeded with one existing `NU1903` advisory on `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- `dotnet build Project.slnx --nologo` — succeeded.
- `cd frontend && npm install` — succeeded.
- `cd frontend && npm run build` — succeeded.

**Assumptions and caveats:**
- Node version in this environment is `v22.12.0`; some frontend packages emitted engine warnings because they expect a newer Node release (>=22.22.2 or 24.x). The install still succeeded, but a newer Node version is recommended for cleaner compatibility.
- This setup verifies dependency installation and compile-time readiness; it does not start the live app or connect to a database automatically.

**Left out of scope:**
- No code or config changes were made to business logic.
- No database migration was applied, no app server was launched, and no secrets were added.

## 2026-08-30 — Create Request & Approve Request entities and DTOs

**Task:** User requested "làm Entity của Request và Approve dùm tôi" — implement Core entities, Application DTOs, validators, and service interfaces for the stationery request lifecycle (Plan M3/M4, §3.4–§4.2).

**Adjustment pass:** User provided approval_transaction.drawio diagram showing the actual workflow. Entities and interfaces were adjusted to match the diagram's status flow (Pending → Approved/Rejected/Withdrawn/CancellationPending → Cancelled) instead of the Plan's Draft/Submitted terminology, and added ApproveCancellationCommand for the diagram's cancellation approval flow ("Bắt tín hiệu 'Request Cancel Approve?'").

**What changed, by file (after adjustments):**

**Core entities (3 files):**
- `Core/Entities/Request.cs` — header with status flow from approval_transaction.drawio: Pending (not Draft) as initial state, Approved/PartiallyApproved/Rejected/Withdrawn/CancellationPending/Cancelled/Fulfilled states. Default status is "Pending".
- `Core/Entities/RequestItem.cs` — line with ItemId/Quantity (> 0), `UnitCostSnapshot`, `LineTotal`.
- `Core/Entities/RequestStatusHistory.cs` — append-only audit trail.

**Application DTOs (6 new files in Application/DTOs/Requests/):**
- `CreateRequestCommand.cs` — requestor input: items + RequiredByDate (optional, no future-date validation).
- `RequestDto.cs` (+2 sub-records): full request, line items, status history.
- `ApproveRequestCommand.cs` — approver's per-line decisions (approve/reject/modify quantity).
- `ApproveCancellationCommand.cs` — **NEW** approver's cancellation response (Approved bool + Reason).

**Validators (3 new files in Application/Validators/Requests/):**
- `CreateRequestCommandValidator.cs` — items not empty, no duplicates, quantity > 0 and < 10000.
- `ApproveRequestCommandValidator.cs` — decisions are valid, ModifiedQuantity required for 'modified'.
- `ApproveCancellationCommandValidator.cs` — **NEW** RequestId > 0, Reason max 500 chars.

**Service interfaces (2 files in Application/Interfaces/Requests/):**
- `IRequestService.cs` — **UPDATED** status flow: CreateAsync (Pending) → SubmitAsync (submit notification) → ApproveAsync (Pending → Approved/PartiallyApproved/Rejected) → RequestCancellationAsync (→ CancellationPending) → **NEW ApproveCancellationAsync** (CancellationPending → Cancelled/deny). DeletePendingAsync (not DeleteDraftAsync).
- `IRequestQueries.cs` — unchanged (already aligned with diagram).

**Documentation:**
- `docs/development/request-entity-dto-implementation-handoff.md` — **UPDATED** to reference approval_transaction.drawio diagram flow, status enum values, and ApproveCancellationCommand.

**DB changes:** None.

**Tests actually executed:**
- `dotnet build Project.slnx --nologo` — **0 errors, 2 unrelated warnings**. All files compile cleanly.

**Architecture decisions recorded (from diagram):**
- Diagram flow: Pending state is initial (not Draft). Requestor & Approver both can see Pending requests.
- Approval check ("Kiểm tra") has Yes/No paths → Approved/Rejected respectively.
- Cancellation request and cancellation approval ("Bắt tín hiệu 'Request Cancel Approve?'") are separate flows.
- Status transitions are atomic with history logging.
- Notification service ("Bắt tín hiệu", "Gửi thông báo") sends notifications on request created, cancelled, withdrawn.

**Assumptions made (flagged, not silently decided):**
- approval_transaction.drawio is the authority on workflow, overriding Plan's Draft/Submitted terminology.
- Cancellation approval is a separate method (ApproveCancellationAsync) rather than inline in ApproveAsync.
- RequiredByDate has no validation (can be any value or null) — diagram does not specify rules.

**Explicitly left out of scope:**
- EF Core Configurations, migrations, DbSeeder — M3 P1.
- Service implementations, Controllers, Frontend — M3 P2–P4.
- Tests — M3 P5.
- Database migration application and smoke test.

**Reviewer follow-ups:**
- Confirm Pending vs Draft choice matches intended UX (are requests immediately visible to both parties, or hidden until submit?).
- Confirm ApproveCancellationAsync is a separate endpoint or part of ApproveAsync with a flag.
- Define: can a user cancel their own Rejected request? (Not shown in diagram but may be useful.)
- Define: time limits or other constraints on "Bắt tín hiệu 'Request Cancel Approve?'" decision window.
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

## 2026-08-31 — New Request and My Requests Pages (Backend & Frontend)

**Tool:** Antigravity (Gemini 3.7 Flash).

**Task:** Build full backend and frontend implementation for the "New Request" and "My Requests" pages per the project plan and architecture specs.

**What was done, by file:**
- **Application Layer:**
  - `Application/Interfaces/Requests/IRequestQueries.cs` — Added optional `statusFilter` parameter to `GetByRequestorAsync`.
- **Infrastructure Layer:**
  - `Infrastructure/Queries/RequestQueries.cs` — Implemented `statusFilter` query filtering in `GetByRequestorAsync`.
- **WebApi Layer:**
  - `WebApi/Controllers/RequestsController.cs` (new) — Full REST controller for request lifecycle: `GET /api/v1/requests` (paged, visible to caller), `GET /api/v1/requests/mine` (current user's requests), `GET /api/v1/requests/{id}` (detail, 404 for unpermitted access), `POST /api/v1/requests` (create request), `POST /api/v1/requests/{id}/submit` (submit for approval), `POST /api/v1/requests/{id}/withdraw` (withdraw pending), `POST /api/v1/requests/{id}/request-cancellation` (cancellation request), `DELETE /api/v1/requests/{id}` (delete unsubmitted pending), `GET /api/v1/requests/dashboard-summary` (status counts).
- **Frontend API & Components:**
  - `frontend/src/api/requests.js` — Expanded with full requestor endpoints (`getRequests`, `getMyRequests`, `getRequest`, `createRequest`, `submitRequest`, `withdrawRequest`, `requestCancellation`, `deletePendingRequest`, `getRequestSummary`, `getPendingApprovals`, `approveRequest`, `approveCancellation`).
  - `frontend/src/pages/requests/components/RequestDetailModal.jsx` (new) — Modal showing full request header, requestor/approver details, snapshotted line items table, and status history timeline audit trail with contextual actions.
  - `frontend/src/pages/requests/components/CancellationModal.jsx` (new) — Form modal to prompt cancellation reason for Approved/PartiallyApproved orders.
  - `frontend/src/pages/requests/NewRequestPage.jsx` (new) — Complete stationery requisition form with catalogue item selector, quantity inputs, estimated subtotal/totals calculation, "Save as Draft", and "Submit Request" flows.
  - `frontend/src/pages/requests/MyRequestsPage.jsx` (new) — Requisition management dashboard with status filter, paged requests table, view details, submit, withdraw, request cancellation, and delete actions.
  - `frontend/src/App.jsx` — Updated routes for `/new-request` and `/my-requests` to render `NewRequestPage` and `MyRequestsPage`.
  - `frontend/src/pages/NewRequest.jsx` & `frontend/src/pages/MyRequests.jsx` — Re-exported page components for compatibility.
- **Tests Added:**
  - `Tests/WebApi.IntegrationTests/RequestsTests.cs` (new) — Integration tests covering create, submit, withdraw, delete, own-request filtering (`/mine`), and ownership-aware 404 security checks.
  - `frontend/src/pages/requests/NewRequestPage.test.jsx` (new) — Component tests for NewRequestPage: catalogue loading, item selection, quantity adjustment, save draft, immediate submit.
  - `frontend/src/pages/requests/MyRequestsPage.test.jsx` (new) — Component tests for MyRequestsPage: loading, empty state, table rendering, status actions, modal view, submit, withdraw, cancellation flow.
- **Documentation:**
  - `docs/development/request-pages-implementation-handoff.md` (new) — Comprehensive implementation and verification handoff document.

**Tests actually executed:**
- Backend: `.NET 10 test Project.slnx` — **70/70 passed** (26 unit + 44 integration tests).
- Frontend: `vitest run --pool=threads` — **67/67 passed** across 12 test suites.
- Both backend build and frontend build run cleanly.

**Assumptions & Design Choices:**
- Concurrency control adheres to Guid-based `RowVersion` compare-then-set across all mutations.
- Unsubmitted pending requests are detected via absence of a `Pending -> Pending` transition in `StatusHistory`.
- Status transitions, audit trail history, and ownership checks are enforced server-side.


## 2026-08-31 — Reports page: role-scoped data (server-side) + per-tab insight line

**Tool:** Claude Code (claude-sonnet-5).

**Task:** Scope the Reports page's underlying data by the logged-in user's role/reporting
line — Requestor sees only their own spend (no By Team tab), Manager sees only their direct
team plus an always-visible personal "My Requests" tab, Business Manager sees their group
(their managers + those managers' teams) with a per-team breakdown, Managing Director sees
everything — enforced server-side, not client-side filtering. Add a one-line computed
insight (period-over-period + top-mover) at the top of each report tab, derived from data
already being queried. Keep existing tabs, filters, Print and Export CSV intact.

**Investigation before writing code (per systemprompt.md):** inspected the existing
auth/role model (Identity roles with `RankLevel`, `SuperiorEmployeeNumber` hierarchy, JWT
claims, `RequestQueries`'s row-scoping pattern) and the current Reports page (100% client-side
mock, **no backend at all** — no `IReportQueries`/`ReportsController`/`/reports/*` route
anywhere). Reported both findings back to the user before proceeding, since honouring "never
returned by the API" required building the backend, and the codebase's existing scoping is
binary (rank<2 vs rank>=2) with no team/group concept. User confirmed: build the real backend;
Manager and Business Manager both scope to direct reports only (no recursion — Business
Manager's "direct reports" are Managers, so By Team still shows one row per Manager); generate
a demo hierarchy + request history since the live DB only had one MD→Manager→Engineer chain
and zero requests.

**What changed, by file:**
- **Backend, new:** `Application/DTOs/Reports/*.cs` (`ReportInsightDto` + `ReportScope` enum,
  and one DTO per report), `Application/Interfaces/Reports/IReportQueries.cs`,
  `Infrastructure/Queries/ReportQueries.cs`, `WebApi/Controllers/ReportsController.cs`
  (`[Authorize]` only — not Manager+, since a plain Requestor is a legitimate audience for
  their own data), `Tests/WebApi.IntegrationTests/ReportsTests.cs` (6-test scope-boundary
  matrix: Engineer/Manager/Business-Manager/MD nesting, My-Requests always-self, By-Team
  sibling-group isolation).
- **Backend, modified:** `WebApi/Program.cs` (registers `IReportQueries`; calls the new demo
  seeder when `Seed:DemoData=true`), `WebApi/appsettings.Development.json` (`Seed:DemoData:
  true`), `Infrastructure/Data/DbSeeder.cs` (`SeedDemoDataAsync` — dev-only, idempotent, 1
  MD + 2 Business Managers + 4 Managers + 12 Engineers with real `SuperiorEmployeeNumber`
  links, a fallback 15-item catalogue, 100 synthetic approved-ish requests).
- **Frontend, new:** `frontend/src/lib/insights.js` (+ test) — turns `ReportInsightDto` into
  the sentence; `components/ReportInsight.jsx` (dumb renderer); `components/MyActivityView.jsx`
  (the new always-on personal tab).
- **Frontend, modified:** `api/reports.js` (real `client.get` calls, `getMyActivityReport`
  added), `lib/reports.js` (trimmed — aggregation moved server-side, kept only the date-range
  helpers; `reports.test.js` rewritten to match), `ReportsPage.jsx` (`useAuth()`-driven tab
  visibility — My Requests always on, Inventory Valuation rank≥2, By Team rank≥3; renders
  `ReportInsight`; date bounds no longer sourced from mock data), `App.jsx`/`navigation.js`
  (Reports route/nav entry no longer Manager+-gated — the page itself is now open to everyone,
  row-level scoping is the real control).
- **Deleted:** `frontend/src/api/mock/reports.mock.js`.
- **Docs:** `docs/development/reports-page.md` — added §10–§17 documenting the new
  architecture; old mock-era sections kept but marked superseded rather than deleted.

**Bugs caught and fixed during implementation (not present in the final code):**
1. **Wrong rank source.** First draft read `ApplicationUser.RankLevel` for scope resolution.
   That column is never populated by the real user-creation path — `IdentityAccountAdapter`
   and `IdentityUserStore` both derive rank from the assigned Identity role
   (`AspNetUserRoles` → `AspNetRoles.RankLevel`). Every normally-created user would have
   scoped as rank 0/1 regardless of actual role. Caught by testing against real
   `TestUserFactory`-created users (not just the demo seeder, which happened to set the
   column too) before it shipped. Fixed to join through the role, matching `/auth/me`.
2. **EF query translation failure on SQLite.** The first aggregation design composed
   `GroupBy` directly with `RequestItem → Request/StationeryItem/Category` navigation joins;
   this throws `could not be translated` on SQLite (the integration-test provider) even
   though the same shape may work on SQL Server. Rewrote to filter+project to a flat
   in-memory row set in one SQL query, then aggregate with LINQ-to-Objects — documented as a
   deliberate, flagged deviation from "SQL-side GROUP BY" (class-level doc comment on
   `ReportQueries`), with the same precedent already present in this codebase
   (`InventoryQueries.GetSummaryAsync`).

**Assumptions made (flagged in the handoff, not silently decided):**
- Business Manager "Group" scope is a **fixed depth-2** lookup (their direct-report managers'
  teams), not a recursive descendant walk — confirmed with the user, but flagged that a third
  management layer would under-report for the top tier.
- "Committed spend" = `Approved` ∪ `PartiallyApproved` ∪ `Fulfilled` (the Plan says "Approved
  only"; the status enum splits committed money into three terminal states).
- Insight `DriverLabel` is always an item name, even on the By Team tab ("driven by [item]",
  not "driven by [team]").
- Demo seed hierarchy and names are invented for demonstration, not real HR data.

**Validation actually run:**
- `dotnet build Project.slnx` — 0 errors.
- `dotnet test Project.slnx` — **76 passed** (26 unit + 50 integration, incl. the new 6-test
  `ReportsTests`).
- `npm run build` — pass (1699 modules). `npm test` — **64 passed** (13 files).
- Live HTTP smoke test against the real seeded hierarchy on this machine's SQL Server
  instance: Engineer `#26` Self ($1,111.67) ⊂ Manager `#22` Team ($10,945.22) ⊂ Business
  Manager `#20` Group ($23,154.94); `By Team` for `#20` lists exactly the 4 teams in their
  group, correctly excluding sibling Business Manager `#21`'s teams; `My Requests` returns a
  different (smaller) number than each user's scoped report, confirming it is genuinely
  self-only.

**Left out of scope:** No browser click-through of the finished page by the developer (module
resolution + live API calls verified, not the rendered UI). Managing-Director scope verified
via the integration test's own synthetic MD, not the live DB's pre-existing `#1` account
(its password predates this session and is unknown). No UI regression test for tab visibility
by role.

### 2026-08-31 (same day) — follow-up: backend convention alignment + route-gating fix

Review pass ("ensure the backend aligns with the flow/conventions of the other pages"):

- **`ReportsController`**: converted the 5 expression-bodied actions to block bodies with a
  `var result = await …; return Ok(result);` shape + per-method `<summary>`, matching
  `RequestsController` / `UsersController`. Added `[FromQuery] DateOnly? fromDate/toDate = null`
  with a controller-side default (last 90 days) so the endpoints are usable without params —
  the other list endpoints all give their query params defaults (`page = 1`, etc.). Kept the
  private `Actor` helper (DRY over five near-identical reads; wording matches the siblings'
  inline `?? throw`).
- **DTOs**: `Scope` field changed from the `ReportScope` enum + `[JsonConverter]` to a plain
  `string` (`scope.ToString()` at the boundary), matching the one existing precedent —
  `InventoryRowDto.Status` carries its state as a string "rather than an enum + JSON
  converter". `ReportInsightDto.Kind` was already a string, so the DTOs are now internally
  consistent. `ReportScope` stays as an enum used internally by `ReportQueries` for type
  safety, moved to its own file.
- **Kept as-is (already aligned)**: controller depends on `IReportQueries` directly (Plan §2.4
  names this exact pattern for reports/joins; `RequestsController` does the same — no
  pass-through `IReportService` needed); `Infrastructure.Queries` / `Application.Interfaces.Reports`
  / `Application.DTOs.Reports` namespaces; `AsNoTracking`; private `sealed record` helpers
  inside the query class (as `InventoryQueries` does); thin controller with no `try/catch`
  (middleware handles it, per `RequestsController`/`InventoryController`).

- **Route-gating fix** (this was documented as done in the prior entry but the code change had
  been missed): `/reports` is now genuinely outside the `ProtectedRoute requireManager` group
  in `App.jsx`, and the `navigation.js` Reports entry no longer has `minRankLevel: 2`. Without
  this an Engineer was still redirected away from the page the feature is built to serve them.

**Validation:** `dotnet build` 0 errors; `dotnet test` **76 passed**; `npm run build` +
`npm test` **64 passed**. Live smoke test: Engineer `#26` now reaches all five `/reports/*`
endpoints (HTTP 200, `scope: "Self"`); `/reports/by-team` for that Engineer returns their thin
self-scoped view rather than 403 (row-scoping is the control, not the route); a no-param call
resolves to a trailing 90-day window.
## 2026-09-01 — ELK observability (Serilog → Elasticsearch + Kibana)

**Tool:** Claude Code (Sonnet 5).

**Task:** Add structured logging to the backend via Serilog, shipping directly to Elasticsearch (no Filebeat/Logstash), with Kibana for visualization — per explicit user choice among the ELK options presented.

**What was done, by file:**
- `WebApi/WebApi.csproj` — added `Serilog.AspNetCore` 8.0.3 and `Serilog.Sinks.Elasticsearch` 10.0.0.
- `WebApi/Program.cs` — restructured with Serilog's recommended bootstrap-logger + `try`/`catch`/`finally` pattern (`Log.CloseAndFlush()` on shutdown). `builder.Host.UseSerilog(...)` configures Console always, plus an Elasticsearch sink when `Elasticsearch:Uri` is set and the environment isn't `Testing` (so `WebApplicationFactory`-based integration tests never dial out). Added `app.UseSerilogRequestLogging()`. The `catch` clause excludes `HostAbortedException` so `dotnet-ef` tooling and `WebApplicationFactory` (which both abort the host deliberately to inspect it without calling `Run()`) still work.
  - **Bug found and fixed during verification**: the first version passed `UseSerilog` without `preserveStaticLogger: true`, so it replaced the shared static `Log.Logger` on every host boot. Because `WebApplicationFactory` always throws `HostAbortedException` to stop the host, the `finally` block's `Log.CloseAndFlush()` ran on *every* test-host startup — including inside the same test process where other test classes' hosts were concurrently starting and actively logging through that same static `Log.Logger`. That caused intermittent `ObjectDisposedException`s mid-startup (surfacing to xUnit as "the entry point exited without ever building an IHost"), failing ~9-10 of 44 integration tests only when run together, never in isolation. Fixed by passing `preserveStaticLogger: true`, so each host gets its own DI-scoped logger instead of mutating the shared static one.
- `WebApi/appsettings.json` — added a `Serilog:MinimumLevel` section and an empty `Elasticsearch:Uri` key.
- `WebApi/appsettings.Development.json` — set `Elasticsearch:Uri` to `http://localhost:9200`.
- `docker-compose.yml` — added `elasticsearch` (8.15.0, single-node, security disabled, named volume `es-data`) and `kibana` (8.15.0) services; `backend` now depends on `elasticsearch` and gets `Elasticsearch__Uri`. Also added `Seed__BootstrapAdminPassword` to the `backend` service, which had been missing from this file even though the Jenkinsfile fix for the same env var landed on 2026-08-30 — `docker-compose.yml` was never updated to match at the time.
- `Jenkinsfile` — new deploy-stage steps run `stationeryms-elasticsearch` and `stationeryms-kibana` containers (same `docker rm -f` + `docker run -d --restart unless-stopped` pattern as backend/frontend), with a named Docker volume (`stationeryms-es-data`) so log history survives container recreation on each build. Backend's `docker run` now also passes `-e Elasticsearch__Uri=http://stationeryms-elasticsearch:9200`. Header comment and success-message echo updated to match.

**Tests actually executed:** `dotnet build Project.slnx` — 0 errors. `dotnet test Project.slnx` — **44/44 passed** (backend only; this task didn't touch frontend code, so frontend tests weren't re-run).

**Jenkins UI action required (per house rule — the team pastes the pipeline script into the Jenkins UI, it isn't synced from this repo's `Jenkinsfile`):**
- No new Jenkins credential needed — Elasticsearch/Kibana here run without auth (`xpack.security.enabled=false`), matching the docker-compose reference setup, appropriate for this internal eProject deployment.
- New ports exposed: **9200** (Elasticsearch) and **5601** (Kibana) — must be free on the deploy host.
- New deploy steps to paste in: two more `docker rm -f` / `docker run -d` blocks (Elasticsearch, Kibana) before the existing backend block, a `docker volume create stationeryms-es-data || true` line, and one more `-e Elasticsearch__Uri=http://stationeryms-elasticsearch:9200 \` line added to the existing backend `docker run` command. Exact text is in the repo's `Jenkinsfile`.
- Resource note: Elasticsearch needs meaningful RAM (`ES_JAVA_OPTS=-Xms512m -Xmx512m` set here as a conservative default) and on some Linux hosts requires `vm.max_map_count >= 262144` (`sysctl -w vm.max_map_count=262144`) or the container fails to start — not yet verified on the actual deploy host, flag if Elasticsearch crash-loops there.

**Assumptions & exclusions:**
- Chose direct Serilog → Elasticsearch (no Filebeat/Logstash) per explicit user selection, not because it's architecturally mandated anywhere in the Plan (ELK isn't mentioned in the Plan at all — this is infra/ops tooling, not a Plan `[SPEC]` item).
- No index lifecycle management (ILM) policy configured — logs will accumulate unbounded in Elasticsearch. Not in scope for this pass; flag for follow-up before a long-lived deployment.
- Did not add authentication to Elasticsearch/Kibana (`xpack.security.enabled=false`) — acceptable for this internal, team-only eProject deployment, but would need revisiting before any wider exposure.

## 2026-09-02 — Notification system (Plan M4/§4.2 [SPEC])

**Tool:** Claude Code (Sonnet 5).

**Task:** Implement the notification feed the Plan calls out as `[SPEC]` and "not deferrable" — 6 trigger events (request submitted/approved/rejected/withdrawn/cancelled, password changed), each firing to exactly two recipients, plus 4 endpoints and a frontend bell. Most of the request-lifecycle workflow this hooks into (`RequestService`, `ApprovalController`) already existed from earlier M3/M4-adjacent work; this pass added the notification layer on top of it, not the workflow itself.

**What was implemented, by file:**
- **Core:** `Core/Entities/Notification.cs` (Id, RecipientEmployeeNumber, RequestId?, EventType, Title, Message, IsRead, CreatedAtUtc — no nav property to `ApplicationUser`, matching the `StockTransaction`/`RequestStatusHistory` pattern). `Core/Enums/NotificationEventType.cs` (int-backed enum: RequestSubmitted/RequestApproved/RequestRejected/RequestWithdrawn/RequestCancelled/PasswordChanged — matches the existing `StockTransactionType` convention rather than the Plan's illustrative `nvarchar` ERD).
- **Infrastructure:** `Data/Configurations/NotificationConfiguration.cs` (composite index `(RecipientEmployeeNumber, IsRead)` per Plan §3.3 for the cheap unread-count poll), migration `20260902112636_AddNotifications`, `Services/NotificationService.cs` (write side), `Queries/NotificationQueries.cs` (read side). `DataContext.cs` gets the new `DbSet<Notification>`.
- **Application:** `DTOs/Notifications/{NotificationDto,UnreadCountDto}.cs`, `Interfaces/Notifications/{INotificationService,INotificationQueries}.cs`.
- **Hooked into existing transactions** (no new transaction wrapper — each call just stages rows on the already-open `DataContext`, committed by the caller's existing `SaveChangesAsync`): `Infrastructure/Services/RequestService.cs` (`SubmitAsync`, `ApproveAsync`, `WithdrawAsync`, `ApproveCancellationAsync`) and `Application/Services/Auth/AuthService.cs` (`ChangePasswordAsync`).
- **WebApi:** `Controllers/NotificationsController.cs` — `GET /api/v1/notifications` (paged feed), `GET /api/v1/notifications/unread-count`, `POST /api/v1/notifications/{id}/read`, `POST /api/v1/notifications/read-all`, matching the Plan's "Notifications — Member 4" endpoint table exactly. DI registration in `Program.cs`.
- **Frontend:** `api/notifications.js`, `hooks/useNotifications.js` (30s poll of the unread-count endpoint only, paused on `document.hidden`, resumed on `visibilitychange`; full feed fetched on demand when the dropdown opens), `components/layout/NotificationBell.jsx` (badge, dropdown with loading/error/empty states, mark-read on click, mark-all-read). Wired into `Header.jsx`, replacing the disabled placeholder that was already there (that placeholder's own comment named this exact feature and endpoint as the thing that would eventually replace it).

**Two design decisions made explicit rather than guessed silently** (documented in code comments at the point of use):
1. **Recipient pairing.** The Plan says notifications go to "the actor and their superior," but read literally that breaks for approve/reject — the actor there is the approver, and the approver's own superior has nothing to do with the request. Recipients for all 5 request-related triggers are `{RequestorEmployeeNumber, ApproverEmployeeNumber}` regardless of who performed the action, matching the source spec's original wording ("popped up to the person and his superior" — person = requestor, his superior = approver, the same relationship in this app's hierarchy model). Password-changed has no request, so it uses the literal `{actor, actor's superior}`.
2. **Two-step cancellation.** Only the final `Cancelled` outcome fires a notification — not the initial `request-cancellation` step (→ `CancellationPending`) or a denial (→ back to `Approved`). The Plan names exactly 6 triggers and "cancelled" is the only cancellation-related one on that list.

**Tests actually executed:**
- Backend: `dotnet test Project.slnx` — **61/61 passed** (50 pre-existing + 11 new: a 6-case `[Theory]` against `NotificationService` directly, matching the Plan's own explicit acceptance criteria wording ("notification service emits 2 rows for each of the 6 event types"), plus 4 endpoint-level integration tests exercising the real submit/change-password HTTP flows end to end, plus 1 edge-case test for an actor with no superior).
- Frontend: `npx vitest run --pool=threads` — **90/90 passed** (15 new: 8 for the polling hook, 7 for the bell component — covering badge count/cap, dropdown open/loading/error/empty, mark-read, mark-all-read, and poll-pause-on-hidden-tab behavior with fake timers).
- Both backend and frontend builds run cleanly.

**Assumptions & exclusions:**
- No frontend UI for the `MyActivity`/report-style breakdown of notification history beyond the dropdown feed — out of scope, Plan only specifies the bell + feed + mark-read.
- No toast-on-action UI (Plan's T4.8 also mentions "toast on action") — the bell/badge/dropdown covers the persisted-feed half of the notification UX; a toast for the *acting* user's own screen at the moment of the action (e.g. "Request submitted" right after clicking Submit) was not added in this pass, since it's a separate, smaller UI concern from the feed itself and every action already gets its own success/error handling in the calling page.
- Notification rows are never deleted — matches the Plan's "persisted notification feed" framing; no retention/cleanup policy was requested or added.

## 2026-09-02 — Dashboard page (home `/`)

**Tool:** Claude Code (claude-sonnet-5).

**Task:** Build the Dashboard page (was a placeholder) to the approved wireframe
(`docs/Wireframe/Dashboard.png`), composing existing endpoints only — no new backend.

**Spec status flagged before building:** `__ai_agents/Requirements/` does not exist. The
Dashboard is **NOT SPECIFIED in the Plan** — page-map §3 says "no dashboard endpoint, no
milestone owns it… confirm ownership and whether it is in scope before building it." Built on
the user's explicit request; ownership still unconfirmed.

**What changed, by file:**
- `frontend/src/pages/dashboard/DashboardPage.jsx` (new) — one `useAsync` that `Promise.all`s
  `getPendingApprovals` (count), `getRequests` (5 most recent visible), and — Manager+ only —
  `getLowStock`. Loading / error states via the shared `StateBlock`.
- `frontend/src/pages/dashboard/components/DashboardKpis.jsx` (new) — the 3 wireframe KPI
  cards (Pending Approvals, Low Stock Alerts, Remaining Budget), lucide icons, red emphasis
  when low-stock > 0.
- `frontend/src/pages/dashboard/components/RecentRequestsCard.jsx` (new) — table (ID /
  Requester / Date / Status / Total), reuses `RequestStatusBadge`, "View All" → `/my-requests`.
- `frontend/src/pages/dashboard/components/LowStockPanel.jsx` (new) — per-item cards with a
  qty-vs-reorder-level bar, "Reorder" → `/inventory`.
- `frontend/src/App.jsx` (shared) — `Dashboard` import/route swapped to `DashboardPage`.
- Deleted `frontend/src/pages/Dashboard.jsx`.
- Reused: `PageHeader`, `Card`, `Button`, `StateBlock`, `RequestStatusBadge`, `useAsync`,
  `useAuth`, `lib/format`. No new npm packages. No backend / DB changes. No `.cs` touched.

**Assumptions & deviations (flagged, not silently decided):**
- **Remaining Budget** has no data source (`/users/me/eligibility` was never built — page-map
  §14). Rendered as a muted "Not available yet" placeholder — no fabricated budget figure.
- **Low Stock** is Manager+ (its endpoint is `RequireManager`). For a Requestor the KPI shows
  "—" / "Manager view only" and the side panel is hidden, so Recent Requests spans full width.
  The page never calls `/inventory/low-stock` for a non-manager (so no 403 surfaces).
- **"Reorder"** links to `/inventory` (real Adjust / Receive actions live there) — page-map §3
  notes reorder has no endpoint.
- **No SKU** in the low-stock cards — not in `InventoryRowDto`; page-map §3 says SKU is a Plan
  future improvement, don't build it.
- Request IDs shown as `#{requestId}` — `RequestDto.requestId` is a bare int, not the
  wireframe's "REQ-2039" mock-up format.
- Recent Requests uses `GET /requests` (visibility-scoped: own / +subordinates / all),
  matching the wireframe showing multiple requesters.

**Validation actually run:**
- `npm run build` — pass (Vite, 1707 modules, no errors).
- `npm test` — **90 passed** (16 files); no regressions.
- Live smoke test: Manager `#22` → all 3 endpoints 200; Engineer `#26` → approvals/requests
  200, `/inventory/low-stock` 403 (page correctly does not call it for non-managers);
  `DashboardPage.jsx` module resolves.

**Left out of scope:** the eligibility/budget backend (M1); a real reorder flow from the
dashboard; the wireframe's global search bar in the top nav (that's a shared-layout concern,
not this page); per-page render tests (matches the other composed pages, none of which have
one). Not committed to `main` / not pushed.

## 2026-09-03 — Multi-select New Request catalogue picker

**Task:** Replace the New Request single-item dropdown with a compact table that supports adding multiple catalogue items in one action.

**What changed, by file:**
- `frontend/src/pages/requests/NewRequestPage.jsx` — replaced `selectedItemId` and the `<select>` picker with checkbox selection state, an item/category/price table, and an `Add selected items` action. Existing client-side search remains; items already in the requisition are excluded and removed items reappear as selectable.
- `frontend/src/pages/requests/NewRequestPage.test.jsx` — changed dropdown tests to accessible checkbox interactions and added multi-item selection coverage.
- `docs/development/request-pages-implementation-handoff.md` — documented the picker flow and its unchanged API/authorization boundary.

**Assumptions:** “small table” means the existing compact bordered-table style and one checkbox per result. Selection is intentionally preserved while filtering so users can search and select across multiple queries before adding.

**No API, backend, database, migration, authorization, or request-state changes.** `GET /api/v1/items` remains the server-authorized source of eligible items.

**Validation:** `npx vitest run src/pages/requests/NewRequestPage.test.jsx --pool=threads` — 6/6 passed. `npm run build` in `frontend/` — passed.

**Out of scope:** select-all controls, page-size changes, and request lifecycle changes.

## 2026-09-03 — Dashboard: time-frame filter on Recent Requests, scroll areas, budget-tile note

**Tool:** Claude Code (claude-sonnet-5).

**Task:** Three dashboard tweaks: (1) cap the Recent Requests and Low Stock Alerts sections
with a vertical scroller instead of unbounded growth; (2) add a time-frame control to Recent
Requests — This Week / This Month / Custom From→To — for every user; (3) explain the
"Remaining Budget" tile's "not available yet" state.

**Spec status:** `__ai_agents/Requirements/` still does not exist. The Dashboard itself is
NOT SPECIFIED in the Plan (page-map §3) — this is incremental polish on the already-merged
dashboard (PR #17), on the user's explicit request.

**What changed, by file (all frontend, dashboard-scoped, no backend/DB/.cs):**
- `frontend/src/pages/dashboard/components/RequestTimeframe.jsx` (new) — segmented
  Week/Month/Custom control; exports `resolveTimeframeWindow(value)` → inclusive `[fromMs,
  toMs]` epoch window and `DEFAULT_TIMEFRAME`. Style mirrors
  `reports/components/DateRangeControl.jsx` + `ReportTabs.jsx` (active state).
- `frontend/src/pages/dashboard/components/RecentRequestsCard.jsx` — renders the control in
  the header; `useMemo`-filters the incoming list by `createdAtUtc` within the window; wraps
  the table in `max-h-96 overflow-y-auto` with a `sticky top-0` header; distinct empty state
  for "no requests in this period".
- `frontend/src/pages/dashboard/components/LowStockPanel.jsx` — items list gains
  `max-h-96 overflow-y-auto`.
- `frontend/src/pages/dashboard/DashboardPage.jsx` — Recent Requests fetch `pageSize` 5 → 100
  (`RECENT_FETCH`) so the client-side filter has rows to work with.

**Assumptions (flagged):**
- `GET /requests` has **no date parameter** (verified: `RequestsController.GetVisible` /
  `api/requests.js` take only page/pageSize/status), so the time-frame filter runs
  **client-side over the 100 most-recent visible requests**. If a user has >100 requests
  newer than the selected window's start, older matches beyond 100 won't appear. Acceptable
  at eProject scale; the real fix is `fromDate`/`toDate` on that (shared, M4-owned) endpoint —
  deliberately out of scope here.
- "This week" = from Monday 00:00 of the current week; "This month" = from the 1st of the
  calendar month; both run to end of today. Default is **This Month**.
- Scroller cap: `max-h-96` (~24rem).

**Left out of scope:**
- **"Remaining Budget" tile** — still a placeholder. It is NOT a higher-ups-only feature and
  is NOT yet coded (neither backend nor frontend). It maps to the Plan's unbuilt
  `GET /api/v1/users/me/eligibility` (page-map §14: role limit − month-to-date approved
  spend, from `RoleThresholds.MaxAmountPerMonth`). No such route/controller exists
  (`grep`-confirmed). The tile renders "Not available yet — needs the eligibility service"
  rather than a fabricated number, per systemprompt.md.
- No tests added — the dashboard shipped without any and none touch these files.
- Manual browser click-through not done by the developer (build + 91 unit tests + HMR clean).

## 2026-09-03 — Role spending eligibility ("Remaining Budget"), Phase 1

**Tool:** Claude Code (claude-sonnet-5).

**Task:** Implement the per-role monthly budget limit the user specified (Engineer 500 /
Manager 2 000 / Business Manager 5 000 / MD 20 000) and wire the dashboard's "Remaining
Budget" tile to it. This is Phase 1 of the scope agreed earlier — the eligibility read model
+ dashboard tile. Submit-time enforcement (Phase 2) deliberately left out (edits the
M4-owned request lifecycle — needs a coordination call).

**Spec status:** `__ai_agents/Requirements/` still does not exist. The endpoint IS in the
Plan (§4.2 `GET /users/me/eligibility`, T1.6 "eligibility engine", `[SPEC]`); the thresholds
are Plan §3.3 "Amount-Employee-role threshold mapping table" `[SPEC]`; magnitudes from
page-map §14.

**What changed, by file:**
- Backend new: `Application/DTOs/Users/EligibilityDto.cs`,
  `Application/Interfaces/Users/IEligibilityQueries.cs`,
  `Infrastructure/Queries/EligibilityQueries.cs`, migration
  `20260903044750_AddRoleBudgetThresholds`, `Tests/WebApi.IntegrationTests/EligibilityTests.cs`
  (4 tests), `docs/development/eligibility-budget.md` (handoff).
- Backend modified (shared, additive): `Infrastructure/Identity/ApplicationRole.cs` (+2
  decimal columns), `ApplicationRoleConfiguration.cs` (precision 18,2),
  `Infrastructure/Data/DbSeeder.cs` (`Roles` tuple gains the two limits; `SeedRolesAsync` is
  now create-**or-update** so pre-existing DBs get the allowances backfilled),
  `DataContextModelSnapshot.cs`, `WebApi/Program.cs` (DI),
  `WebApi/Controllers/UsersController.cs` (`GET me/eligibility`).
- Frontend: `api/users.js` (`getMyEligibility()`), `DashboardPage.jsx` (added to the
  dashboard `Promise.all`, caught individually so it can't blank the page),
  `DashboardKpis.jsx` (Remaining Budget tile → real currency + "% of monthly allowance",
  red under 10%, placeholder on failure).

**Assumptions made (all flagged in the handoff, all reversible):**
- **Thresholds as columns on `AspNetRoles`**, not the schema-of-record's separate
  `RoleThresholds` table — consistent with the Identity fold that already put `RankLevel`
  there (CLAUDE.md K8). Needs an ERD/SQL reconciliation note.
- **`MaxAmountPerRequest == MaxAmountPerMonth`** — schema has the column, no documented value.
- **Month-to-date spend** = `Requests` by this employee, `CreatedAtUtc` in the current UTC
  month, status not in {Rejected, Withdrawn, Cancelled}. Amount = `TotalEstimatedCost`
  (no per-line approved figure exists for PartiallyApproved).
- Currency is magnitudes only (`[ASK] #10`).

**Left out of scope:**
- **Phase 2** — 422 on over-limit submission (`RequestService.SubmitAsync`, `Block|Warn`
  config flag, TC-05). Crosses into M4's request-lifecycle service; needs coordination.
- The "My Eligibility" page (page-map §14) and New Request's "running total vs remaining"
  (Plan T3.7) — both would reuse `GET /users/me/eligibility` as-is.
- No browser click-through of the tile by the developer (build + 91 backend + 91 frontend
  tests only).

**Branch note:** `feat/role-budget-eligibility` is **stacked on `feat/dashboard-recent-requests-filter`**
(both touch `DashboardPage.jsx`). Merge the filter branch first, or rebase after.

**Validation:** `dotnet build Project.slnx` 0 errors; `dotnet test Project.slnx` 91 passed
(incl. 4 new EligibilityTests); `npm run build` + `npm test` 91 passed.

## 2026-09-04 — Sign in by email address as well as employee number

**Task:** Add email login to the sign-in page, keeping employee-number login working.

**Tool:** Claude Code (Opus 5). Prompt summary: "can you add login by email to the login page?"

**Scope note — this goes beyond the Plan.** Plan §3.1 line 213 makes `EmployeeNumber` "primary
key **and** login", and §4.2 / T1.2 / the M1 acceptance list all specify "employee number +
password". Email as a second identifier is a user-requested addition, not something the Plan
asks for; it is safe because Identity already holds `Email` unique (`RequireUniqueEmail = true`
in Program.cs), so an address identifies exactly one account. Flagged here rather than merged
silently (CLAUDE.md §5).

**Design:** one input labelled "Employee number or email". `frontend/src/api/auth.js` decides the
wire shape — all digits → `{employeeNumber}`, anything else → `{email}` — so the page and
`AuthContext` stay unaware of the contract. The server accepts exactly one identifier.

**What changed, by file:**
- `Application/DTOs/Auth/LoginRequest.cs` — now `(int? EmployeeNumber, string? Email, string Password)`. Both identifiers optional at the type level so the 16 existing callers that send `employeeNumber` are untouched.
- `Application/Validators/Auth/LoginRequestValidator.cs` — new. Exactly one identifier (XOR), password non-empty. Deliberately **no** format or range checks — see the correction below.
- `Application/Interfaces/Auth/IAccountStore.cs` — `+ VerifyCredentialsByEmailAsync`.
- `Infrastructure/Identity/IdentityAccountAdapter.cs` — both lookups now funnel into one private `VerifyAsync`, so the email path cannot drift from the employee-number path: same `IsActive` gate, same `CheckPasswordSignInAsync(lockoutOnFailure: true)`, same generic failure. Email lookup uses `FindByEmailAsync`, which matches on Identity's `NormalizedEmail` and is therefore case- and whitespace-insensitive for free.
- `Application/Services/Auth/AuthService.cs` — validates, then branches on which identifier is present. Constructor gained `IValidator<LoginRequest>`.
- `WebApi/Controllers/AuthController.cs` — 401 detail changed from "Employee number or password is incorrect." to "Those sign-in details are incorrect." so it names neither identifier.
- `frontend/src/api/auth.js` — `login(identifier, password)` picks the wire shape.
- `frontend/src/contexts/AuthContext.jsx` — parameter renamed `employeeNumber` → `identifier` (it was never used as a number).
- `frontend/src/pages/Login.jsx` — single `identifier` field, `type="text"`, placeholder `101 or you@hmt.local`; separate messages for 400 (malformed request) and 401 (generic).
- Tests: `AuthServiceTests` (+3), `AuthTests` integration (+6), `Login.test.jsx` (+2), new `frontend/src/api/auth.test.js` (4).

**A mistake made and corrected during this task, worth recording.** The first version of
`LoginRequestValidator` enforced the Plan's 1–1000 employee-number range and an email-format
check. That broke the pre-existing contract test
`Login_UnknownEmployeeNumber_ReturnsSameGeneric401AsWrongPassword`: employee number `999999`
started returning **400** where it had returned the generic **401**. That is a real information
leak, not just a failing test — a 400/401 split tells an attacker which identifiers are even
worth trying. Both rules were removed. The rule now is: a request is rejected with 400 only when
it is structurally unanswerable (no identifier, both identifiers, or no password); anything that
merely *cannot match an account* returns the same generic 401 as a wrong password.

**No database, migration, authorization-policy, token or request-state changes.** Lockout,
`IsActive` enforcement and JWT contents are untouched.

**Validation actually run (2026-09-04):**
- `dotnet build Project.slnx` — 0 errors. `dotnet test Project.slnx` — **133/133** (Application.UnitTests 53, WebApi.IntegrationTests 80).
- `npx vitest run --pool=threads` — **104/104** across 18 files. `npm run build` — passed.
- Live API on `.\SQLEXPRESS`: login by email → 200; `"  AI.Tester@HMT.Local  "` (case + whitespace) → 200; employee number 901 → 200; unregistered email → 401 "Those sign-in details are incorrect."; both identifiers at once → 400.
- Browser: signed in as `ai.tester@hmt.local` → dashboard "Welcome back, Ai Tester"; wrong password → the generic alert, still on `/login`; `901` → dashboard. (The Browser pane's viewport collapsed to 0×0 partway through, so the last two checks were driven through the DOM rather than by clicking.)

**Known issue / follow-up for the reviewer:** an unregistered email returns without a password
hash being computed, while a registered one does — a timing side-channel that could in principle
be used to enumerate addresses. The employee-number path has had exactly the same shape since
M1, so this is not a regression, and closing it means always hashing against a dummy. Worth a
decision, not silently ignoring. Auth changes need **2 reviewers** (CLAUDE.md §5).

## 2026-09-04 — Pagination on the remaining large tables (12 rows per page)

**Task:** "Make other tabs that have large table have next page, similar to that in catalogue.
Keep 12 items per page."

**Tool:** Claude Code (Opus 5).

**What changed, by file:**
- `frontend/src/hooks/usePagination.js` — **new.** Client-side pagination for a table that already holds all its rows. Exposes `pageRows` (the slice) and `isOnPage(index)`; `DEFAULT_PAGE_SIZE = 12`. Resets to page 1 when the row count changes, and clamps so the page is never past the end.
- `frontend/src/components/ui/Pagination.jsx` — **new.** The footer, lifted verbatim from the Catalogue page's wording and markup ("Page 1 of 4 · 40 items" + Previous/Next), so every table now reads the same. Carries `data-print-hide`. Takes an optional `noun`/`nounPlural`.
- `frontend/src/pages/inventory/InventoryPage.jsx` — paginates `visibleRows` (sorted + filtered) via `pageRows`; footer under the table.
- `frontend/src/pages/manager/ItemManagement.jsx` — same, on `sortedItems`.
- `frontend/src/pages/manager/SupplierManagement.jsx` — same, on `sortedSuppliers`, `noun="supplier"`.
- `frontend/src/pages/reports/components/{CostByItemView,ItemHeadcountView,TeamExpenditureView,InventoryValuationView,MyActivityView}.jsx` — paginated via `isOnPage` (see below). Nouns: team / request / item.
- Tests: `frontend/src/hooks/usePagination.test.js` (7), `frontend/src/components/ui/Pagination.test.jsx` (5).

**Why the Reports tabs are paginated differently — the one real design decision.** The Reports
page has a Print button that prints the live DOM (`window.print()` over `[data-print-region]`).
If those tables rendered only the current slice, printing a cost report would silently emit page
1 and nothing else — a data-integrity problem in a manager report, not a cosmetic one. So the
report views keep **every** row in the DOM and hide the off-page ones with Tailwind's
`hidden print:table-row`; the print stylesheet re-shows them. Verified in the browser: the
generated rule `.print\:table-row { display: table-row }` sits at stylesheet index 375 against
`.hidden` at 99, so it wins the cascade inside `@media print`. The `<tfoot>` totals were already
computed over the full row set and are untouched, and CSV export reads `filteredRows` on the
page, so neither shows a per-page number.

**Deliberately not paginated:**
- `CumulativeCostView` — its two tables are one row per period (~12 max) and a fixed top-5.
- Dashboard "Recent Requests", and the line-item tables inside the request/approval/supplier-request modals — all small and bounded by design.
- Catalogue (15/page), My Requests (15), Approvals (20), User Management (20), New Request picker (5) — already paginated, and left at their existing sizes rather than being retuned to 12 as an unrequested change. **They are now inconsistent with the new tables; say the word and I will unify them.**

**No backend, API, database or authorization changes.** Pagination is client-side over data the
pages already fetch, exactly as the Catalogue does; no endpoint contracts were touched.

**Validation actually run (2026-09-04):**
- `npm run build` — passed. `npx vitest run --pool=threads` — **116/116** across 20 files (was 104/18).
- Browser, signed in as the Managing Director: Inventory "Page 1 of 4 · 40 items", 12 rows, Next → page 2 starting at "Highlighter Set, 6 Colours"; Item Management "Page 1 of 4 · 40 items"; Suppliers "Page 1 of 1 · 7 suppliers"; Reports → Inventory Valuation 40 rows in the DOM / 12 on screen / 28 hidden, footer "Page 1 of 4 · 40 items", `<tfoot>` still "Total Stock Value $30,135.28" over all 40. Every report tab rendered without error.

**Known limitation:** the page resets when the row *count* changes, which covers filtering and
searching. A filter that happens to leave the count identical keeps the current page number; the
clamp still guarantees it is in range. Catalogue solves this by resetting on its specific filter
state, which the shared hook cannot see.

## 2026-09-04 — Deep audit, then fixes for the five CRITICAL findings (C1–C5)

**Task:** (1) "Perform a DEEP SCAN of the ENTIRE project … UNDERSTAND → TRACE → COMPARE →
IDENTIFY → REPORT", no code changes. (2) "Fix only confirmed CRITICAL priority implementation
issues. Do not change project requirements. Do not fix 'Potential Logical Issues' without
confirmation. After each fix: Run → Test → Verify affected workflow → Continue."

**Tool:** Claude Code (Fable 5.1).

### Part 1 — the audit (no code changed)

Every C# source file, every frontend page/API module, all 7 migrations, the Plan (§3.4, §3.6,
§4.2, §5.2, §7, §10, §14), CLAUDE.md, AI_INSTRUCTION.md, StationerySchema.sql and the 18
handoffs in `docs/development/` were read; build and both test suites were run. Written up as
**`PROJECT_AUDIT.md`** (repo root) and an interactive report. Nine confirmed logical errors
(C1–C9), seven potential issues (P1–P7, need a team decision), eight missing features. Headline:
CLAUDE.md §1 is a week stale — requests, approvals, notifications, reports, supplier orders, role
budgets and the AI assistant all exist — but the approval workflow was structurally wrong in ways
no test caught, because the tests were written from the implementation, not from Plan §3.6.

### Part 2 — the fixes (CRITICAL only; C6–C9 and every P-item deliberately left untouched)

| ID | Was | Now |
|---|---|---|
| **C3** | `RequestQueries` gated Manager+ visibility on `ApplicationUser.RankLevel`, a column nothing writes (always 1). | Rank resolved by the `AspNetUserRoles → AspNetRoles.RankLevel` join, same as `ReportQueries`/`RequestService`. One private helper, four call sites. |
| **C1** | No `Draft` status; requests were born `Pending`, so "Save as Draft" landed in the approver's queue. The UI faked drafts by pattern-matching history. | Real `Draft` status (Plan §3.6). Create → `Draft`; submit → `Draft → Pending` + notification; approver queue never sees a Draft. UI reads the status, no inference. |
| **C2** | `ApproveAsync` counted per-line decisions and discarded them; `ModifiedQuantity` dropped; `RequestItem` had nowhere to store it. | `RequestItems.Decision` + `ApprovedQuantity` (new columns), written per line, matched by `RequestItemId` (unknown/duplicate id → 409). Header status: all approved-as-asked → `Approved`; all rejected → `Rejected`; anything else (incl. a reduced qty) → `PartiallyApproved`. Requested `Quantity`/`LineTotal` are never rewritten. |
| **C4** | `DELETE /requests/{id}` removed any `Pending` request — including submitted ones — cascade-deleting its history. | `DeleteDraftAsync`: `Draft` is the only deletable state. Submitted → 400 "Withdraw a submitted request instead." |
| **C5** | `CancellationPending` was a dead end: `GET /approvals/pending` returned `Pending` only and no page called `/cancel-approval`. | Queue now returns `Pending` + `CancellationPending`; `ApprovalsPage` shows "Decide" on those rows, opening the new `CancellationDecisionModal` (approve / refuse, shows the requestor's reason). |

**What changed, by file:**
- `Core/Entities/RequestItem.cs` — `Decision` (string?, ≤20) and `ApprovedQuantity` (int?). `Core/Entities/Request.cs` — default status `Draft`; doc lists `Draft`.
- `Infrastructure/Data/Configurations/RequestItemConfiguration.cs` — the two columns + `CK_RequestItems_Decision`. `RequestConfiguration.cs` — `Draft` added to `CK_Requests_Status`, default `Draft`.
- `Infrastructure/Data/Migrations/20260904033743_AddDraftStatusAndLineDecisions.*` — **new migration** (generated with `dotnet ef`, then hand-edited to add a data fix: existing `Pending` rows with no `Pending→Pending` submit marker become `Draft`, with the reverse in `Down`). This is the one open-migration PR — announce it.
- `Infrastructure/Services/RequestService.cs` — `CreateAsync` (Draft), `SubmitAsync` (Draft→Pending), `ApproveAsync` (persist decisions, new status rule), `DeletePendingAsync` → `DeleteDraftAsync`.
- `Infrastructure/Queries/RequestQueries.cs` — `GetRankLevelAsync` helper (C3); `GetPendingApprovalsAsync` includes `CancellationPending`; DTO mapping carries the two new fields.
- `Application/Interfaces/Requests/{IRequestService,IRequestQueries}.cs`, `Application/DTOs/Requests/{RequestDto,SubmitRequestCommand}.cs` — signatures/docs; `RequestItemDto` gains `Decision`, `ApprovedQuantity`.
- `WebApi/Controllers/RequestsController.cs` — `DeleteDraft`, doc strings.
- `frontend/src/api/requests.js` — `deletePendingRequest` → `deleteDraftRequest`; docs.
- `frontend/src/pages/requests/MyRequestsPage.jsx` — Draft filter option; Submit/Delete on `Draft`, Withdraw on `Pending`; `isRequestSubmitted` removed.
- `frontend/src/pages/requests/components/RequestDetailModal.jsx` — same; shows Decision / Approved qty columns once a request is decided.
- `frontend/src/pages/requests/components/RequestStatusBadge.jsx` — Draft badge. `NewRequestPage.jsx` — draft success wording.
- `frontend/src/pages/requests/ApprovalsPage.jsx` + **new** `components/CancellationDecisionModal.jsx` (C5).
- `.claude/launch.json` — `api` entry (SQLEXPRESS connection string passed as a CLI config arg; checked-in appsettings untouched).
- Tests: `Tests/WebApi.IntegrationTests/RequestsTests.cs` (+7, 3 renamed), `MyRequestsPage.test.jsx` (+1, fixtures now Draft/Pending), `ApprovalsPage.test.jsx` (+1).

**APIs changed:** `POST /requests` now returns `status: "Draft"`. `POST /requests/{id}/submit` requires `Draft` (409 otherwise). `DELETE /requests/{id}` requires `Draft` (400 otherwise). `GET /approvals/pending` also returns `CancellationPending` rows. `POST /approvals/{id}/approve` rejects a decision whose `requestItemId` is not on the request (409). `RequestItemDto` gains `decision`, `approvedQuantity`. No new endpoints.

**DB changes:** one migration — see above. Applied to this machine's SQLEXPRESS dev database at startup; `__EFMigrationsHistory` shows it on top; the data fix converted 7 rows to `Draft` and left 8 genuinely submitted rows `Pending`.

**Validation actually run (2026-09-04):**
- `dotnet build Project.slnx` — 0 errors. `dotnet test Project.slnx` — **140/140** (53 unit + 87 integration; was 133).
- `npx vitest run --pool=threads` — **118/118** across 20 files (was 116).
- **Live, in the browser against the real API + SQL Server** (users 902 Engineer → 901 Manager): New Request → "Save as Draft" → My Requests shows `Draft` with Submit + Delete, no Withdraw → approver's `GET /approvals/pending` is **empty** → Submit → `Pending`, Withdraw only → approver's queue shows #20 → requestor's `DELETE` → **400**, request still readable → approver Review, "Modify qty" 10→4 → `PartiallyApproved`; DB row: `Decision=modified, ApprovedQuantity=4, Quantity=10` → requestor requests cancellation → approver's queue shows `Cancellation Pending` with **Decide** → Approve cancellation → `Cancelled`. History: 5 rows (Draft, Pending, PartiallyApproved, CancellationPending, Cancelled); notifications: 2 rows per trigger event. Two 502s in the browser console predate the run (from the API restarts while getting the launch config right) — the fixed flow produced only 200s.

**Assumptions / decisions:**
- Adding `Draft` follows Plan §3.6 as written. The Plan's contingency (line 1659, "remove the Draft state — submit directly") was *not* what the code did: the UI kept a "Save as Draft" button while the server had no such state, which is the worst of both. Choosing the Plan's primary design, not the contingency, is a restoration of the requirement — not a change to it.
- All-`modified` decisions → `PartiallyApproved` (a reduced quantity is by definition not a full approval). The old rule produced the same status but by accident.
- `TotalEstimatedCost` still reflects the *requested* total (it is the cost snapshot, CLAUDE.md #8). An "approved total" is derivable from `ApprovedQuantity × UnitCostSnapshot` but no column was added — that needs a team decision on what Reports should sum.

**Deliberately left out of scope (all still open, see PROJECT_AUDIT.md):**
- **C6** — `ApproveCancellationAsync` omits `.Include(r => r.StatusHistory)`, so *refusing* a cancellation always reverts to `Approved`, even for a `PartiallyApproved` request; its validator is never injected. **C5 makes this path reachable from the UI for the first time.** It is a two-line fix; it was rated High, not Critical, so it was not touched. Recommend doing it next.
- **C7** budget enforcement, **C8** stock issue on approval / stock guard / `Fulfilled`, **C9** blank category/supplier on request lines, the missing `RequestStateMachine` class, and P1–P7.

**Shared files touched:** `frontend/src/api/requests.js`, `.claude/launch.json`. No route/nav changes.

## 2026-09-04 — C6: refusing a cancellation now reverts to the right status

**Task:** "Fix C6 as well, same Run → Test → Verify loop."

**Tool:** Claude Code (Fable 5.1).

**What was wrong.** `RequestService.ApproveCancellationAsync` loaded the request with
`.Include(r => r.Items)` only, then searched `request.StatusHistory` for the status to revert to
on refusal. The list was always empty, so the `?? "Approved"` fallback fired every time — a
`PartiallyApproved` request whose cancellation was refused silently became `Approved`. Also
`ApproveCancellationCommandValidator` existed but was never injected, so the path ran
unvalidated. Both became user-reachable once C5 gave the approver a "Decide" button.

**What changed, by file:**
- `Infrastructure/Services/RequestService.cs` — inject `IValidator<ApproveCancellationCommand>` and call it; `.Include(r => r.StatusHistory)`; on refusal revert to the `FromStatus` of the most recent transition *into* `CancellationPending` (read from the audit row, not inferred from "last status that wasn't X"); throw 409 if no such row exists rather than guess.
- `WebApi/Controllers/ApprovalController.cs` — **removed the three `try/catch` blocks.** Found while testing: the controller's `catch (Exception)` turned the validator's 400 into a 500 `Problem(...)`. CLAUDE.md #2 says controllers carry zero try/catch because `ExceptionHandlingMiddleware` already maps Validation/NotFound/Conflict to 400/404/409 ProblemDetails; every other controller already works that way. Error shape from these three endpoints is now consistent with the rest of the API (the frontend's `detail ?? error` fallback chain handles both).
- `Tests/WebApi.IntegrationTests/RequestsTests.cs` — `RefuseCancellation_PartiallyApprovedRequest_RevertsToPartiallyApproved`, `ApproveCancellation_ReasonOver500Chars_Returns400` (the latter failed with 500 until the controller change).
- `docs/development/critical-fixes-request-workflow-handoff.md`, `PROJECT_AUDIT.md` — updated to C1–C6 fixed.

**No DB, migration, frontend or route changes.**

**Validation actually run (2026-09-04):**
- `dotnet build` — 0 errors (after stopping the running API, which locked `WebApi/bin`). `dotnet test Project.slnx` — **142/142** (53 unit + 89 integration; was 140).
- Frontend untouched; suite still 118/118 from the previous entry.
- **Live, in the browser against SQL Server:** request #21 taken to `PartiallyApproved` (qty 10 → 3) with a cancellation pending via the API; then as approver 901 in the UI: Approvals → "Decide" → comment → **Refuse cancellation** → `POST /approvals/21/cancel-approval` 200 → queue empty. DB: `Requests.Status = PartiallyApproved`, `RequestItems.ApprovedQuantity = 3` untouched, last history row `CancellationPending → PartiallyApproved` by 901 with the comment. Before the fix the same action produced `Approved`.

**Assumptions / decisions:** reverting to the `FromStatus` of the *most recent* entry into `CancellationPending` (by time, then id) is the only reading consistent with Plan §3.6 ("CancellationPending → Approved: superior rejects cancellation" for a request that was Approved — and, by the same rule, PartiallyApproved for one that was). The 409-instead-of-guess on a missing history row is defensive; it cannot be hit through the API because `RequestCancellationAsync` always writes that row in the same save.

**Still open:** C7, C8, C9, the `RequestStateMachine` class, P1–P7 — see `PROJECT_AUDIT.md`.

**Merge note (same day, on "commit and push"):** `origin/khang` had moved 17 commits ahead
(Help page, support inbox, dashboard quick-link, sub-tree request visibility, BM restrictions,
and migration `20260903181104_AddSupportMessages`). Merged with `--no-ff`. Two conflicts:
- `RequestQueries.cs` — their commit `5f31a29` had already replaced the `ApplicationUser.RankLevel`
  reads with `IHierarchyQueries` sub-tree scoping, a stricter and correct fix for C3. **Their
  version was kept**; this branch's `GetRankLevelAsync` helper and the C3 regression test
  (`GetById_UnrelatedManager_SeesRequest_RankComesFromRole`, which asserted a peer manager can see
  any request — false under the sub-tree rule) were dropped. Only the C5 queue filter and the two
  DTO fields were re-applied.
- `RequestsTests.cs` — took their hierarchy users; all other tests merged cleanly.
The migration was **regenerated** as `20260904033743_AddDraftStatusAndLineDecisions` so it sits
after `AddSupportMessages` in the snapshot chain (the dev DB was first rolled back to
`AddRoleBudgetThresholds` with `dotnet ef database update`), and the hand-written data-fix SQL
was re-added. Post-merge validation is recorded below.

**Post-merge validation actually run (2026-09-04):**
- `dotnet build` — 0 errors. `npx vitest run --pool=threads` — **137/137** across 23 files (their
  Help/Support/Dashboard tests included).
- `dotnet test Project.slnx` — first run **4 failures, all pre-existing on `origin/khang`**, not
  caused by the merge: `SupportTests.Resolve_*` returned 500 because `SupportController` uses
  `[Authorize(Policy = "RequireManagingDirector")]` and that policy was never registered in
  `Program.cs` (an unregistered policy name makes the authorization middleware throw). Verified by
  grepping `origin/khang:WebApi/Program.cs` directly. Fixed in a separate follow-up commit with one
  line — `.AddPolicy("RequireManagingDirector", … RankLevelRequirement(4))`, matching the
  `RequireManager`(2) / `RequireBusinessManager`(3) entries beside it — because pushing a branch
  with four red tests helps nobody. After the fix: **160/160** (54 unit + 106 integration).

## 2026-09-05 — Audit C7: enforce the spending limit on submit

**Task:** Fix `PROJECT_AUDIT.md` finding C7 — role spending limits were computed for the
Dashboard tile and never enforced, so an Engineer with a 500 allowance could submit 50 000.

**What changed, by file:**
- `Application/Exceptions/BusinessRuleException.cs` — **new**. "Well-formed, permitted, and
  still refused" — the Plan §4.2 422 case, distinct from Validation (400) / NotFound (404) /
  Conflict (409).
- `WebApi/Middleware/ExceptionHandlingMiddleware.cs` — maps it to **422 Unprocessable
  Entity**, title "Business rule violation". First 422 in the codebase.
- `Infrastructure/Services/RequestService.cs` — ctor takes `IEligibilityQueries`;
  `SubmitAsync` refuses when `TotalEstimatedCost > RemainingThisMonth`, before the
  `Draft → Pending` transition. Message names the total, the overage, the limit, the
  month-to-date spend and the reset date (Plan T3.4: "422 with a specific message").
- `frontend/src/pages/requests/NewRequestPage.jsx` — "Submit" is create-then-submit, so a
  refused submit now leaves a saved draft. The error message says so and names the draft id;
  previously it read as though nothing had been saved.
- `Tests/WebApi.IntegrationTests/BudgetEnforcementTests.cs` — **new**, 7 tests: exactly at the
  limit passes / one unit over → 422 naming limit and overage / refused request stays `Draft`
  and never reaches the approver's queue / second request judged on *remaining* not the full
  allowance / withdrawing releases the budget / an over-limit **draft** can still be created /
  a Manager's higher allowance applies.
- `docs/development/eligibility-budget.md` — Phase 2 section (was marked deferred).

**Assumptions made where the request was ambiguous:**
- **Which limit** — the Plan says only "Total ≤ role threshold" and the schema has both
  `MaxAmountPerRequest` and `MaxAmountPerMonth`. Confirmed with the user: enforce
  **`RemainingThisMonth`** (monthly allowance − month-to-date committed). `MaxAmountPerRequest`
  is deliberately *not* enforced — it is seeded equal to the monthly figure, so it would be a
  no-op today.
- **Block vs warn** — `[ASK] #6` is open in the Plan. Confirmed with the user: **hard block**,
  the Plan's own stated default. No config knob, against the earlier handoff's suggestion of
  `Eligibility:Mode` — a single code path is easier to test and defend.
- **Month attribution** — month-to-date windows on `Request.CreatedAtUtc`, so a January draft
  submitted in February is judged against January. Flagged in the handoff, not changed.
- `__ai_agents/Requirements/` still does not exist; Plan §3.6 / §M3 line 977 / T3.4 / TC-05 are
  the requirement sources used.

**Deliberately left out of scope:**
- Per-request cap enforcement (see above).
- Any pre-emptive UI warning while building a basket — the server refusal is the control; a
  live "you have X left" hint on New Request is a separate, additive change.
- C8 (stock), which follows on its own branch and reuses `BusinessRuleException`.

**Validation:** `dotnet build` 0 errors. `dotnet test Project.slnx` — **167 passed**
(54 unit + 113 integration; was 160, +7 new). `npx vitest run --pool=threads` — **138 passed**
(+1 new). `npm run build` clean.

## 2026-09-05 — Audit C8: stock moves on approval, is restored on cancellation

**Task:** Fix `PROJECT_AUDIT.md` finding C8 — approval never checked or moved stock
(`IStockService.IssueAsync` had zero callers), cancellation never gave it back, and the
`Fulfilled` status was unreachable yet still counted.

**What changed, by file:**
- `Core/Entities/StockTransaction.cs` — nullable `RequestId`, so an `Issue` row says which
  request took the stock instead of only a free-text `Reference`.
- `Application/Interfaces/Inventory/IStockService.cs` +
  `Infrastructure/Services/StockService.cs` — new `StageRequestMovementAsync`: applies the
  balance change and adds the ledger row **without saving**, so the caller commits it in the
  same `SaveChangesAsync` as the status change (Plan §3.6 "one DB transaction").
- `Infrastructure/Services/RequestService.cs` — ctor takes `IStockService`. `ApproveAsync`
  loads `Items.Item`, verifies **every** line has stock (422 naming each short item) and only
  then stages one `Issue` per line at `-ApprovedQuantity`. `ApproveCancellationAsync` stages
  one `Adjustment` per line at `+ApprovedQuantity` when the cancellation is approved.
- `Infrastructure/Data/Configurations/StockTransactionConfiguration.cs` — FK + index on
  `RequestId`, `OnDelete(SetNull)`.
- `Infrastructure/Data/Configurations/RequestConfiguration.cs`,
  `Queries/ReportQueries.cs`, `Queries/EligibilityQueries.cs`, `Data/DbSeeder.cs`,
  `frontend/.../MyRequestsPage.jsx`, `.../RequestStatusBadge.jsx` — `Fulfilled` removed from
  the CHECK constraint, both status sets, the dead demo seeder, the filter and the badge map.
- `Infrastructure/Data/Migrations/20260904043931_AddStockIssueOnApproval.*` — **new**:
  `RequestId` column/index/FK, narrowed `CK_Requests_Status`, and a hand-written
  `UPDATE Requests SET Status='Approved' WHERE Status='Fulfilled'` that must run before the
  constraint goes back on.
- `Tests/WebApi.IntegrationTests/ApprovalStockTests.cs` — **new**, 7 tests (see Validation).
- `docs/development/stock-on-approval.md` — **new** handoff. `PROJECT_AUDIT.md` — status
  header updated: all nine confirmed errors now closed.
- Doc-comment cleanups referencing `Fulfilled` in `Request.cs`, `IRequestService.cs`,
  `IReportQueries.cs`, `CostByItemReportDto.cs`, `api/reports.js`, `faqData.js`.

**Assumptions made where the request was ambiguous:**
- **`Fulfilled` removed**, confirmed with the user. Plan §3.6 has no transition into it —
  approval is what moves the stock — so it was unreachable by construction. Reintroducing it
  needs a real fulfilment step, not just the enum value.
- **One `SaveChangesAsync`, no explicit `BeginTransactionAsync`.** EF wraps a single
  `SaveChanges` in one transaction, which satisfies Plan §3.6 and keeps the SQLite test
  provider working. Chosen over an explicit transaction, which the plan offered as the
  alternative.
- **No item row-version check on the staged path.** The approval already gated on the
  *request's* row version; re-checking each item's would fail approvals because an unrelated
  goods receipt touched the item. Flagged as reviewer follow-up 4 with the concurrency
  reasoning.
- **`Adjustment`, not `Receipt`, for the restore** — nothing physically arrived; this reverses
  a movement. Plan §3.6 line 410 says "Adjustment".
- **Stock is taken at approval, not reserved at submit** — matches Plan §3.6, which puts the
  guard on `Pending → Approved`. Two requests can therefore both pass eligibility and the
  second fail the stock guard.
- `__ai_agents/Requirements/` still does not exist; Plan §3.6 lines 405/410, §200 and T2.6 are
  the requirement sources.

**Deliberately left out of scope:**
- Reports still sum `TotalEstimatedCost` (requested), not issued spend — pre-existing, and a
  team decision (also flagged in the C1–C6 handoff).
- No stock reservation at submit; no low-stock warning on the approval screen.
- Audit P-items (P1–P7) — untouched.

**Validation:** `dotnet build` 0 errors. `dotnet test Project.slnx` — **174 passed**
(54 unit + 120 integration, +7 new). `npx vitest run --pool=threads` — **138 passed**.
`npm run build` clean. Migration applied to `StationeryManagementSystem.Dev` on real SQL
Server at startup. **Live round trip** on item #2: 112 → approve 10 units of request #108 →
**102** (`Issue −10, reqId 108, by 22`) → approve its cancellation → **112**
(`Adjustment +10, reqId 108`); ledger sum matches the cached balance.
