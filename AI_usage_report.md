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
