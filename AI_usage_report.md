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
