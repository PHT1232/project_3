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
