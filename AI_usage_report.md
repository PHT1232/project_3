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
