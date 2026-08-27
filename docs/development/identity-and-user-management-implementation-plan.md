# Sign-In, Logout, and User Management Implementation Plan

> Planned 2026-08-27 against `main` at `d64e333`.
>
> Scope order: sign-in → session restoration and logout → authorization → user-management API → user-management UI.

## 1. Architecture decision

Use the full ASP.NET Core Identity framework for credential and role management while keeping JWT bearer tokens as the API authentication mechanism.

Use:

- `IdentityUser<int>` through an Infrastructure-owned `ApplicationUser` type.
- `IdentityRole<int>` through an Infrastructure-owned `ApplicationRole` type.
- `IdentityDbContext<ApplicationUser, ApplicationRole, int>`.
- `UserManager<ApplicationUser>` for creating, updating, activating, and validating users.
- `RoleManager<ApplicationRole>` for role setup and lookup.
- `SignInManager<ApplicationUser>` or `UserManager.CheckPasswordAsync` for credential verification.
- Identity password hashing, validators, security stamps, lockout support, and EF stores.
- JWT bearer authentication for API requests; do not use Identity cookies or Identity UI.

This replaces the earlier custom `PasswordHasher`, custom user store, and custom role store plan. JWT creation remains project-owned because Identity does not issue JWTs.

### Clean Architecture boundary

ASP.NET Core Identity and EF Core types remain in `Infrastructure`. `Core` and `Application` must not reference `Microsoft.AspNetCore.Identity` or `Microsoft.EntityFrameworkCore`.

- `Infrastructure/Identity/ApplicationUser.cs` extends `IdentityUser<int>` and contains domain persistence fields such as `EmployeeNumber`, `Name`, `SuperiorEmployeeNumber`, `IsActive`, and location if confirmed.
- `Infrastructure/Identity/ApplicationRole.cs` extends `IdentityRole<int>` and contains `RankLevel`.
- `Application` defines auth/user use-case interfaces and DTOs.
- `Infrastructure` adapters implement those interfaces with `UserManager`, `RoleManager`, and `SignInManager`.
- Controllers depend on Application services only.

This overrides the Plan's specified custom design (`Core/Entities/{User,Role,RoleThreshold}.cs`, `Core/Interfaces/{IUserRepository,IPasswordHasher,ITokenService}.cs`, plain `PasswordHasher<T>` — Plan lines 172, 217, 850). It is not a detail gap; it changes the auth architecture the Plan specified. **Logged and resolved as [CLAUDE.md §6 K8](../../CLAUDE.md), confirmed by the user 2026-08-27: keep Identity.** The next time the Plan itself is edited, add this to its revision history the same way the .NET 10 switch (K7) was recorded — the Plan currently still describes the custom design and is now stale on this point.

Full Identity inheritance cannot live in `Core` without violating the inward dependency rule, which is why `ApplicationUser`/`ApplicationRole` sit in `Infrastructure` rather than at the Plan's specified `Core/Entities/User.cs` path.

### Schema footprint reconciliation (K8 follow-up, not yet done)

Identity generates `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens` — seven tables replacing the single `Users` table in the 12-table ERD and `StationerySchema.sql:33-48`. Before the migration in §3 is announced, reconcile:

- the ERD (`docs/Diagrams/ERD_project.png`) still shows one `Users` table — it needs updating or an explicit note that it's stale for auth;
- `Users.RoleId` was a FK to a `Roles` lookup table; confirm whether `ApplicationRole` fully replaces it or whether a `RoleThresholds`-style table still needs to exist alongside `AspNetRoles`;
- any other table with a FK to `Users.EmployeeNumber` (e.g. `Requests.RequestorEmployeeNumber`, `StockTransactions`) must now point at `AspNetUsers.Id` — enumerate those tables before the migration, not during it;
- `Users.Grade NVARCHAR(50)` (schema line 40) has no home in `ApplicationUser` as currently scoped in §1/§3 — either add it to `ApplicationUser` or explicitly mark it out of scope for this milestone.

## 2. Identity key and employee-number mapping

Use Identity's integer `Id` as the employee number so the JWT `sub` and route `{empNo}` remain the same identifier.

Configure `ApplicationUser.Id` as `ValueGeneratedNever()` because employee numbers are HR-assigned, not SQL identities. Enforce:

- primary key range check: `Id BETWEEN 1 AND 1000`;
- API property `employeeNumber` maps to `Id`;
- JWT `sub` is `Id`;
- superior FK references `ApplicationUser.Id`;
- API/import superior value `0` maps to database `NULL` and vice versa.

Do not add a second employee-number column unless EF/Identity constraints prove the direct key mapping unworkable during a spike.

## 3. Database and packages

### Packages

Add compatible .NET 10 packages:

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` to `Infrastructure`;
- `Microsoft.EntityFrameworkCore.SqlServer` and design-time tooling to `Infrastructure`;
- `Microsoft.AspNetCore.Authentication.JwtBearer` to `WebApi`;
- FluentValidation packages to `Application`/`WebApi` as required by project conventions;
- test packages for xUnit, FluentAssertions, Moq, `WebApplicationFactory`, and SQLite in-memory.

### Identity model configuration

Configure Identity's generated tables with deliberate names and constraints. Preserve standard security columns such as normalized username/email, password hash, security stamp, concurrency stamp, lockout, access-failure count, claims, roles, logins, and tokens.

Use employee number as `UserName` for Identity lookup. Keep normalized username unique. Configure:

- `Name` as `nvarchar(15)`;
- `Email` as `nvarchar(25)` and required;
- normalized email unique;
- self-referencing nullable superior relationship with restricted delete;
- `IsActive` required;
- role `RankLevel` required;
- role thresholds as a separate domain table if included in this milestone.

Create one reviewed EF Core migration. Announce it before opening the PR because only one migration PR may be active.

## 4. Sign-in backend

### Application contracts

Add DTOs and interfaces for:

- `LoginRequest(employeeNumber, password)`;
- `LoginResponse(accessToken, expiresAtUtc, user)`;
- `CurrentUserDto(employeeNumber, name, email, role, rankLevel, superiorEmployeeNumber, isApprover)`;
- token generation abstraction;
- Identity account-store abstraction;
- current-user abstraction.

The Application service orchestrates the use case without seeing Identity types.

### Infrastructure adapter

The Identity account adapter:

1. Finds the user by employee-number username or integer ID.
2. Rejects inactive, unknown, locked-out, or invalid-password users with the same result.
3. Uses Identity password verification and optionally records failed attempts through lockout support.
4. Loads assigned role and its `RankLevel`.
5. Loads superior and approver information safely when superior is null.
6. Returns an Application-owned account projection.

Do not expose `PasswordHash`, security stamps, concurrency stamps, token rows, or Identity entities through controllers.

### JWT service

Create JWTs with:

- HS256;
- eight-hour expiry;
- `sub = employeeNumber`;
- role claim;
- `rankLevel` claim;
- issuer and audience validation;
- signing key from environment/configuration, never committed;
- zero clock skew.

### API

Add:

- `POST /api/v1/auth/login` with `[AllowAnonymous]`;
- `GET /api/v1/auth/me` with `[Authorize]`.

Invalid login returns `401 ProblemDetails` with one generic message. `/auth/me` returns the documented profile fields and handles the MD's null superior.

## 5. Logout and frontend session

There is no logout endpoint in the approved endpoint catalogue. JWT revocation and refresh-token rotation are cut. Logout is therefore local session destruction:

1. Remove the access token from `localStorage`.
2. Clear `AuthContext` user state.
3. Ensure axios no longer sends the bearer token.
4. Navigate to `/login` with history replacement.

Add:

- `frontend/src/api/auth.js`;
- `frontend/src/contexts/AuthContext.jsx`;
- `frontend/src/routes/ProtectedRoute.jsx`;
- real `frontend/src/pages/Login.jsx`;
- account menu and logout action in `Header.jsx`.

Update the shared axios client with a request interceptor. On app startup, restore a stored token by calling `/auth/me`. Invalid or expired tokens clear the session. Preserve the requested route through login. Protect all application routes.

Remove or redirect `/signup`: self-registration is not in scope. Every frontend fetch must include loading, error, and empty states where applicable.

## 6. Authorization

Configure JWT bearer as the default authentication scheme. Do not configure Identity application cookies as the API scheme.

Add policies:

- `RequireManager`: authenticated user with `rankLevel >= 2`;
- `RequireApprover`: authenticated user who has direct reports.

**Open, unresolved:** whether "approver" should instead be a precomputed claim set at login (vs. a live has-direct-reports check per request) is not decided. Default to the live check for M1; flag this the same way as the initial-password-policy assumption in §7 rather than treating it as settled.

Add `ICurrentUserService` to parse and validate employee number, role, and rank claims. Manager endpoints require the policy at controller level and repeat target/hierarchy checks in Application services.

An Engineer calling a Manager endpoint must receive `403`; unauthenticated callers receive `401`.

## 7. User-management API

### Identity-backed operations

Use `UserManager` and `RoleManager` for:

- creating users and assigning one role;
- validating duplicate username/email;
- hashing initial passwords;
- updating account fields;
- changing role membership;
- activating/deactivating users;
- preserving security and concurrency semantics.

Do not delete users. `PATCH /users/{empNo}/status` updates `IsActive`. On deactivation, update the security stamp so existing authentication state is invalidated where the API validates it. Because existing JWTs are otherwise stateless for up to eight hours, decide and document whether protected requests check active status on each request. Recommended: an `OnTokenValidated` active-user check for immediate deactivation enforcement.

### Validation

Enforce:

- employee number 1–1000;
- name regex `^[\p{L}\p{M} .'-]{1,15}$`;
- email at most 25 characters and unique;
- existing role;
- existing superior unless API value is `0`;
- no self-supervision;
- hierarchy-cycle rejection by walking at most ten superior links.

Initial-password rules are not explicitly specified. Proposed default is the documented change-password policy: minimum eight characters, uppercase, lowercase, and digit. Confirm before implementation or flag it as an assumption.

### Endpoints

- `GET /api/v1/users?page=1&pageSize=20&role=&location=` — Manager+.
- `POST /api/v1/users` — Manager+.
- `PUT /api/v1/users/{empNo}` — Manager+.
- `PATCH /api/v1/users/{empNo}/status` — Manager+.
- `GET /api/v1/users/{empNo}/subordinates` — self or Manager+.

`Users.Location NVARCHAR(100) NULL` exists in `StationerySchema.sql:41`, so the column itself is not `NOT SPECIFIED`. What's unresolved is whether it's a K2/K3-style guess (not present anywhere else — Plan, ERD, wireframes) or an intentional field; until that's confirmed, implement the filter against the existing column but don't treat its presence as Plan-sanctioned.

Use explicit DTO mapping. Return `ProblemDetails`: `400` validation/cycle, `401` unauthenticated, `403` unauthorized, `404` missing user, and `409` duplicate/concurrency conflict.

## 8. User-management UI

Replace the placeholder page with:

- paged user table;
- role/status filters;
- create/edit form;
- superior selector;
- activate/deactivate confirmation;
- direct-subordinates view;
- loading, error, empty, saving, and validation states.

Show the navigation item only to Manager+, but do not treat navigation hiding as authorization. Add a Manager route guard and handle API `403`.

No user-management wireframe exists. Reuse established Tailwind tokens and shared UI components.

## 9. Change-password follow-up

The requested order focuses on sign-in/logout and then user management, but the M1 acceptance scope also requires `POST /auth/change-password`. Implement it immediately after the core user-management slice or include it in the auth slice if scope permits.

Use `UserManager.ChangePasswordAsync`, apply the confirmed password policy, update the security stamp, and create both required notification rows in one transaction when notification infrastructure exists. Do not claim TC-14 complete until both recipients are verified.

## 10. Tests

### Unit tests

- Login orchestration returns a token for valid account projections.
- Unknown, inactive, locked, and bad-password failures remain generic.
- JWT contains `sub`, role, and `rankLevel` and expires in eight hours.
- Name, email, and employee-number constraints.
- Self-supervision and hierarchy-cycle rejection.
- MD/null-superior mapping.
- API `0 ↔ NULL` superior mapping.

### Integration tests

Use `WebApplicationFactory<Program>` with EF Core SQLite in-memory, not the EF InMemory provider.

- Valid login returns JWT and profile.
- Invalid login returns generic `401 ProblemDetails`.
- Password in the Identity user table is hashed, never plaintext.
- `/auth/me` without a token returns `401`.
- `/auth/me` returns role, rank, superior, and approver status.
- Engineer calling `/users` receives `403`.
- Manager can create and list users.
- Duplicate employee number/email is rejected.
- Cycle update returns `400`.
- MD with null superior loads safely.
- Deactivated user cannot log in; existing-token behavior matches the chosen active-user validation design.

### Frontend tests

Add Vitest and React Testing Library:

- successful and invalid sign-in;
- session restoration through `/auth/me`;
- anonymous protected-route redirect;
- expired-token cleanup;
- logout removes token and redirects;
- Manager-only navigation;
- user list loading, error, empty, and populated states;
- create/edit validation and status confirmation.

## 11. Delivery sequence

1. Identity packages, model, `IdentityDbContext`, configuration, and migration.
2. Identity adapter, JWT service, login, `/auth/me`, middleware, and integration tests.
3. Frontend `AuthContext`, login, protected routes, restoration, and logout.
4. Manager policy, current-user service, and active-user token validation.
5. User CRUD, hierarchy queries, cycle prevention, and API tests.
6. User-management UI and frontend tests.
7. Change password and notification integration.
8. Final validation and documentation.

Keep PRs below 400 lines where practical. Auth changes require two reviewers.

## 12. Completion documentation

After implementation:

1. Append a dated entry to root `AI_usage_report.md`; never overwrite existing history.
2. Update this document or create a separate implementation handoff under `docs/development/` containing actual files changed, API and DB changes, setup, usage, tests actually run, assumptions, exclusions, known issues, and reviewer follow-ups.
3. Do not mark planned tests or behavior as completed unless execution proves them.

## 13. Current baseline and blockers

- Repository pulled to `d64e333`.
- `dotnet build Project.slnx --no-restore` passed on SDK `10.0.111` with one existing `NU1903` warning for `Microsoft.OpenApi`.
- Frontend build did not run because dependencies are not installed: `vite: command not found`.
- Existing `bin/` and `obj/` files are tracked/modified and should be cleaned through a separate reviewed repository-hygiene change, not silently overwritten during identity work.

## 14. Implementation handoff (2026-08-27)

All 8 delivery steps in §11 were implemented in one session, each as its own commit. Full file-by-file detail is in the dated `AI_usage_report.md` entry for 2026-08-27 ("Implement sign-in, logout, authorization, and user management") — this section is the architecture/flow summary for a reviewer picking this up cold.

### Architecture as built

Matches §1–§9 as planned, with one addition not anticipated in the plan: `Infrastructure` now carries a `ProjectReference` to `Application` (needed so `IdentityAccountAdapter`/`IdentityUserStore`/`JwtTokenService` can implement Application-owned interfaces) and a `FrameworkReference` to `Microsoft.AspNetCore.App` (needed for `SignInManager`/`IHttpContextAccessor`, which aren't available to a plain `Microsoft.NET.Sdk` class library). Dependency direction is still inward-only — `Core`/`Application` reference nothing new.

`AccountProjection` (Application-owned) is the seam between Identity and the rest of the app for auth; `UserDto`/`IUserStore` is the equivalent seam for user management. Neither leaks `PasswordHash`, security stamps, or Identity types past `Infrastructure`.

### APIs added

`POST /api/v1/auth/login`, `GET /api/v1/auth/me`, `POST /api/v1/auth/change-password`, `GET/POST /api/v1/users`, `PUT /api/v1/users/{empNo}`, `PATCH /api/v1/users/{empNo}/status`, `GET /api/v1/users/{empNo}/subordinates`.

### DB changes

One migration, `InitialIdentity` (`Infrastructure/Data/Migrations/20260827133027_InitialIdentity.cs`): the 7 standard ASP.NET Identity tables, `AspNetUsers` extended with `Name`/`Grade`/`Location`/`SuperiorEmployeeNumber`/`IsActive`/`CreatedAtUtc`, `AspNetRoles` extended with `RankLevel`, a `CK_Users_EmployeeNumber` check (1–1000), and a self-referencing FK on `SuperiorEmployeeNumber`. **Not applied to a real SQL Server instance** — none was available in this environment. `Program.cs` now runs `Database.MigrateAsync()` on startup outside the `Testing` environment, so it will apply automatically the first time the app runs against a real database.

### Setup and usage

1. .NET 10 SDK (`10.0.111`+) and a SQL Server instance reachable via `ConnectionStrings:DefaultConnection`.
2. Set `Jwt:SigningKey` via the `Jwt__SigningKey` environment variable (never commit a real one — `appsettings.Development.json` ships an insecure local-only placeholder, `appsettings.Testing.json` ships a test-only one).
3. `dotnet run --project WebApi` — applies the migration and seeds the 4 roles (Engineer/Manager/Business Manager/Managing Director, ranks 1–4) on startup.
4. No demo users are seeded. Create the first user directly via `UserManager` (see `Tests/WebApi.IntegrationTests/TestUserFactory.cs` for the pattern) or temporarily relax the `RequireManager` policy to bootstrap one through the API.
5. Frontend: `cd frontend && npm install && npm run dev`.

### Tests actually run

- `dotnet test Project.slnx`: **30/30 passed** (17 `Application.UnitTests`, 13 `WebApi.IntegrationTests` against `WebApplicationFactory<Program>` + real EF Core SQLite in-memory).
- `npx vitest run` (frontend): **15/15 passed**.
- `npm run build` and `dotnet build Project.slnx`: both succeed.
- **Not done:** running against a live SQL Server, or a manual browser smoke test. Nobody has clicked through this in a real browser yet.

### Assumptions carried into the build

- Initial/change-password policy (8+ chars, upper+lower+digit) — proposed, not confirmed.
- `RequireApprover` = live "has direct reports" check, not a login-time claim (§6 resolution).
- `location` filter implemented against `Users.Location`, which exists in `StationerySchema.sql` but isn't Plan-sanctioned (K5).

### Explicitly out of scope

- **TC-14 is not complete.** Change-password does not notify the user and their superior — no notification infrastructure exists yet. Flagged in a code comment on `AuthService.ChangePasswordAsync`; do not mark TC-14 done from this work.
- `RoleThresholds` (spend-limit table) — out of scope for this milestone.
- Live-database migration and manual QA (see Tests above).
- K8's schema-footprint reconciliation against the 12-table ERD (§1 of this doc) is flagged but not done — the ERD still shows a single `Users` table.

### Known issues

- Two bugs were found and fixed by the tests, not just exercised by them:
  1. `JwtBearerOptions.MapInboundClaims` defaulted to `true`, remapping the `sub` claim on every validated token — broke `/auth/me`, `ICurrentUserService`, and `RequireApprover` for **every** authenticated request after login, in production as much as in tests. Fixed with `MapInboundClaims = false`.
  2. `AuthContext`'s restore effect depended on `token`, so `login()` re-triggered a redundant `/auth/me` fetch right after login. Fixed by running that effect once on mount only.
- `IdentityUserStore.GetUsersAsync`/`ToDtoAsync` does one role lookup per user per page (not batched) — fine at the Plan's stated scale (~25–1000 users) but worth revisiting if that scale assumption changes.

### Reviewer follow-ups

1. Confirm the password policy with the team.
2. Decide whether `RequireApprover` should move to a login-time claim.
3. Reconcile the Identity table footprint with the ERD (K8).
4. Apply the migration to a real SQL Server and do a manual smoke test before merging.
5. Two reviewers required (auth change, per `CLAUDE.md` §5).
