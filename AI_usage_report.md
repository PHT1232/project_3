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
