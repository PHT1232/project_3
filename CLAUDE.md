# CLAUDE.md — Project Memory

> Last synchronised with `origin/main` at **93e3b25** on **2026-08-28** (branch `khang`, rebased
> onto and pushed to match `origin/main` the same day — see [AI_usage_report.md](AI_usage_report.md)).
> This file is a *pointer and reconciliation* layer, not a copy of the documentation.
> The detailed source of truth is **the Plan** (§7). Read it; don't paraphrase it from memory.
> Anything not verifiable from a project document is marked **NOT SPECIFIED** — never replace
> that marker with a guess.

---

## 1. Project Overview

Web-based **Stationery Management System** for *HMT Technologies*, replacing an email/Excel
stationery request process. Aptech eProject, **5 developers, 3 weeks** (Mon 10 Aug – Fri 28 Aug
2026), delivered as a single container: ASP.NET Core API + React SPA served from `wwwroot`.

Covers: full request lifecycle, role-based spending eligibility, dual-party notifications on six
triggers, three manager cost reports, and one user-facing AI feature.

**✅ Implementation status — verified 2026-08-28 (branch `khang`, content-identical to `origin/main`).**
(Supersedes every earlier "nothing is built" / "pre-M0" note in this file — those described a
branch state that no longer exists after the 2026-08-28 rebase.)

- **Auth & Identity — implemented and tested (M1).** Full ASP.NET Core Identity
  (`ApplicationUser`/`ApplicationRole` : `IdentityUser<int>`/`IdentityRole<int>`,
  `IdentityDbContext<ApplicationUser,ApplicationRole,int>`) with a project-owned JWT layer on top
  (`JwtTokenService`, `IdentityAccountAdapter`, `IdentityPasswordService`). One EF migration
  (`20260827133027_InitialIdentity`) generates the standard `AspNetUsers`/`AspNetRoles` tables
  carrying the domain fields (`Name`, `Grade`, `Location`, `SuperiorEmployeeNumber`, `IsActive`,
  `CreatedAtUtc`, `RankLevel`), a `CK_Users_EmployeeNumber` check (1–1000), and the
  self-referencing superior FK. **Not yet applied to a real SQL Server database** — see below.
  Endpoints live: `POST /api/v1/auth/login`, `GET /api/v1/auth/me`,
  `POST /api/v1/auth/change-password`. `RequireManager`/`RequireApprover` policies plus an
  active-user check on token validation are wired in `Program.cs`.
- **User management — implemented and tested.** `UsersController`
  (`GET/POST /api/v1/users`, `PUT /api/v1/users/{empNo}`, `PATCH /api/v1/users/{empNo}/status`,
  `GET /api/v1/users/{empNo}/subordinates`) plus a full CRUD UI at
  `frontend/src/pages/users/UserManagementPage.jsx`.
- **Frontend auth — implemented.** `AuthContext`, `ProtectedRoute`, a real `Login` page
  (`SignUp.jsx`/`AuthPlaceholder.jsx` deleted — self-registration isn't in the Plan), logout via
  the header account menu.
- **Frontend product pages — implemented, still on mock data.** Catalogue and Inventory
  (`pages/catalogue/`, `pages/inventory/`) are built with loading/error/empty states, but read
  from `src/api/mock/*.mock.js`, not the live API. Several other pages remain 5-line
  `PagePlaceholder` stubs (Dashboard, New Request, My Requests, Approvals, Reports, Suppliers,
  Help).
- **Tests exist and pass (verified 2026-08-28):** backend `dotnet test Project.slnx` —
  **32/32 passed** (17 `Tests/Application.UnitTests` + 15 `Tests/WebApi.IntegrationTests`, the
  latter via `WebApplicationFactory<Program>` + real EF Core SQLite in-memory, `appsettings.Testing.json`).
  Frontend `npx vitest run --pool=threads` — **15/15 passed** across 4 files
  (`AuthContext`, `Login`, `ProtectedRoute`, `UserManagementPage`).
  ⚠️ **On this machine, the default `npm test` (`vitest run`, forks pool) hangs and times out** —
  "Timeout waiting for worker to respond." Use `npx vitest run --pool=threads` instead; this looks
  like a local process-spawning restriction, not a test bug — re-check on other machines before
  assuming it's universal.
- **Never smoke-tested against a live server.** No SQL Server instance was available when this
  work was done, so the migration has never been applied to a real database and the API has never
  been run end-to-end in a browser — only through the test suite. Do not treat this as verified
  outside of tests. (This machine now has SQL Server Express running locally — see §2 — so that
  gap is closeable here.)
- **Everything else is still pre-M0/M1**: no request lifecycle, no notifications, no reports, no
  AI feature, no `RoleThresholds`. `Core/Interfaces/IRepository.cs` and the generic
  `Application/Services/Service.cs` are unused scaffolding, not yet wired to anything real.
- Full detail: [docs/development/identity-and-user-management-implementation-plan.md](docs/development/identity-and-user-management-implementation-plan.md)
  and the 2026-08-27/28 entries in [AI_usage_report.md](AI_usage_report.md).

---

## 2. Technology Stack

Per the Plan §1.2.4, §2.2, §9 — **not** per the current `.csproj`/`README`, which are stale.

| Layer | Technology |
|---|---|
| Backend | **.NET 10**, ASP.NET Core, Clean Architecture (`Core`/`Application`/`Infrastructure`/`WebApi`) — **team decision 2026-08-24, overrides the Plan's .NET 8; see K7** |
| Database | **SQL Server**, EF Core (10.0.10), migrations only — never hand-edit the DB. Dev connection string targets **LocalDB** (`(localdb)\mssqllocaldb`), see below |
| Frontend | **React 18** + Vite + **Tailwind** + React Router + axios; `AuthContext` for session, **no Redux** |
| Auth | ASP.NET Core **Identity** (kept, overrides the Plan's custom design — see K8) issuing a project-owned **JWT** (HS256, `sub` = EmployeeNumber, `Jwt:ExpiryHours` config, default 8h); token in `localStorage` (documented trade-off, Plan §9.2) |
| Validation | FluentValidation (Application layer) — implemented for auth/user DTOs |
| Errors | one `ExceptionHandlingMiddleware` → RFC 7807 `ProblemDetails` — implemented |
| Logging | `ILogger<T>` + Serilog (console + rolling file) — **not yet added**, still the ASP.NET default logger |
| Testing | xUnit · FluentAssertions · Moq; integration via `WebApplicationFactory<Program>` + EF Core **SQLite in-memory**; Vitest + RTL — **all now real and passing**, see §1 |
| CI/CD | Jenkins + Docker, **now two independent containers** (`WebApi/Dockerfile` + `frontend/Dockerfile`/nginx, orchestrated by root `docker-compose.yml`) rather than the single combined image the root `Dockerfile` still describes — both paths exist, the independent one is what Jenkins builds now |

**✅ The .NET toolchain builds and tests cleanly on this machine (verified 2026-08-28).**
`dotnet restore && dotnet build Project.slnx` — 0 errors (1 pre-existing `NU1903` warning on
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11, used by the integration tests). `dotnet test` — 32/32 passed.
`bin/`+`obj/` are still committed repo-wide (`.gitignore` has no .NET section, except a narrow
`Tests/**/bin|obj` carve-out added 2026-08-27) — a known, still-open hygiene gap, not a blocker.

**Config keys that now exist** (`WebApi/appsettings.json` / `appsettings.Development.json` /
`appsettings.Testing.json`): `ConnectionStrings:DefaultConnection` (empty in the base file — set
via environment/secrets in real deployments; a LocalDB dev value is checked into
`appsettings.Development.json`, `SigningKey` too, explicitly marked "LOCAL-DEV-ONLY") and
`Jwt:{Issuer,Audience,ExpiryHours,SigningKey}`. `docker-compose.yml` expects
`DB_CONNECTION_STRING` / `JWT_SIGNING_KEY` from the environment, never hardcoded.

**This machine's dev environment (audited 2026-08-28):** .NET SDK 10.0.400 · Node 24.20.0 / npm
12.0.2 · Git 2.55.0, GitHub reachable over **HTTPS** (the remote was SSH and unreachable before
that day) · **`dotnet-ef` 10.0.11 installed** · a **SQL Server Express** service
(`MSSQL$SQLEXPRESS`) is installed and running, but the dev connection string wants **LocalDB**
specifically (`sqllocaldb` not found here) — either install LocalDB or repoint the dev connection
string at `Server=.\SQLEXPRESS`, a deliberate choice for whoever does it next. Docker and `gh` CLI
are still absent.

---

## 3. Main Roles

Capability comes from **role + position in the reporting hierarchy**, not separate user types.
Hierarchy: Engineer → Manager → Business Manager → Managing Director, via self-referencing
`Users.SuperiorEmployeeNumber`. Top of hierarchy is **`NULL`** (the spec's `0` is mapped
`0 ↔ NULL` at the API/import boundary — Plan §3.1).

- **Requestor** — every employee. Catalogue, requests, own status, eligibility, withdraw, cancel.
- **Approver** — the employee listed as the requestor's superior. Also a requestor.
- **Manager and above** (`RankLevel ≥ 2`) — plus cost reports, master data, AI forecast.

---

## 4. Architecture Principles

Do not relitigate these in a task; they are `[DECISION]`s in the Plan.

1. **Dependencies point inwards, always.** `Core` → nothing. If `using Microsoft.EntityFrameworkCore;`
   appears in `Core` or `Application`, the abstraction leaked — reject the PR.
2. **Controllers are thin.** Model-bind → call service → map to `ActionResult`. Zero business
   logic, zero `try/catch`, zero `DbContext`.
3. **Explicitly rejected — do not introduce** (Plan §2.4): MediatR/CQRS · a generic `UnitOfWork`
   over `DbContext` (call `SaveChangesAsync` in the service) · AutoMapper (write `ToDto()`) ·
   SignalR (poll `unread-count` every 30s) · soft delete on every table (`IsActive` only on
   `Users`, `StationeryItems`, `Suppliers`).
4. **`IRepository<T>` is for simple by-id/CRUD only.** Reports and joins use explicit named query
   interfaces (`IReportQueries.GetCostByItemAsync()`) implemented in Infrastructure.
5. **Stock is a ledger, not a counter.** `QuantityAvailable` is a cached balance;
   `StockTransactions` is append-only truth. Every balance change writes a matching ledger row
   *in the same DB transaction*.
6. **State changes are one atomic transaction** — status + history + stock + both notification
   rows commit or roll back together.
7. **Never `DELETE` a submitted request.** Status transitions only; `Draft` is the sole deletable
   state. Only `RequestStateMachine.Transition()` may write `Request.Status`.
8. **Costs are snapshotted** (`RequestItems.UnitCostSnapshot`) so price edits never rewrite history.
9. **Authorisation is server-side and ownership-aware** — a policy on the controller *and* a
   row-level check inside the service. Not your request → **404, not 403** (don't leak existence).
10. **The AI feature degrades, never fails.** Deterministic core first; the LLM only narrates;
    10s timeout + one retry + fallback. **The demo must work with the network unplugged.**
11. **All timestamps UTC**, converted at the UI boundary. Money is `decimal(18,2)`, never float.

---

## 5. Important Development Rules

- **Read the Plan section before designing.** Quote the label (`[DECISION — Plan §2.4]`) when
  justifying a choice.
- **Do not invent** requirements, entities, statuses, endpoints, or pages. Silent on it → say
  `NOT SPECIFIED` and ask. **Never silently resolve a conflict** between documents (§8).
- **Respect `[CUT]`** (Plan §1.3). Building a cut item is a scope breach: email/SMTP · SignalR ·
  refresh-token rotation · microservices · trained ML model · file uploads/images ·
  multi-language · dark mode · Redis · Kubernetes · PO generation · mobile app.
- **Git:** trunk-based, no `develop`. Branch `<type>/<milestone>-<kebab>`, **max 2 days life**,
  rebase before PR, **squash merge**, PR **< 400 lines**, conventional commits. Self-merge
  prohibited. 2 reviewers for auth / workflow / stock. **Only one open PR may contain an EF
  migration at a time** — announce it.
- **AI agents never commit, push, merge, rebase, or branch unless explicitly told to.**
- Never commit secrets, connection strings, `.env`, `bin/`, `obj/`, `node_modules/`.
- **Report only what you actually ran.** If you couldn't build or test, say why.
- Every UI data-fetch needs **loading, error and empty** states — happy-path-only is incomplete.
- **If you cannot explain it, do not merge it.** The rubric zeroes unexplainable AI-generated code.

---

## 6. Current Open Conflicts — Do Not Resolve Alone

Most of the earlier conflicts were **resolved** by the Plan (see §9). These remain:

| # | Conflict | Status |
|---|---|---|
| **K1** | **`ReturnedForModification` — 7 vs 8 statuses.** Plan §3.6/§4.2 (`POST /requests/{id}/return`), T4.3 and `frontend.md` all include a *return for modification* branch. `docs/Diagrams/request_diagrams_v3.drawio` declares it **out of scope by team decision** and says the Plan "must be updated to match". Plan is dated 07 Aug but committed 24 Aug; the diagram's file date is 19 Aug. **Neither is obviously newer.** | **Blocking** for the request enum, `/return`, and whether an approver has 2 or 3 outcomes |
| **K2** | **`StationerySchema.sql` contradicts Plan §3.1** — `EmployeeNumber INT IDENTITY(1,1)` with no `CHECK 1–1000`; `Name NVARCHAR(200)` vs `nvarchar(15)`; `EmailId NVARCHAR(256)` vs `nvarchar(25)`. `AI_usage_report.md` flags these as assumptions. Plan's are `[SPEC]` = non-negotiable → **Plan wins; the SQL needs fixing.** | Flagged |
| **K3** | **Invented status/type values in SQL.** `Requests.Status` CHECK allows `PartiallyApproved` and `Fulfilled` (in no other document) and omits `Draft`/`Withdrawn`/`CancellationPending`. `StockTransactions.TxType` = `Inbound/Outbound/Adjustment/Return` vs the Plan's `Receipt/Issue/Adjustment`. Self-flagged guesses → **Plan wins.** | Flagged |
| **K4** | **Migrations vs raw SQL.** Plan §9.3: "EF Core migrations only — never edit the database by hand." But `__ai_agents/Database/*.sql` exists and `systemprompt.md` calls it "schema of record". Reference-only or deployment path? | NOT SPECIFIED |
| **K5** | **Wireframe fields with no column and no Plan concept:** `Department` (New Request + Approvals filter). `SKU` appears in three wireframes but the Plan lists "barcode/SKU" as *future improvement* → **don't build it.** `Notify Me` and the `MGR APPROVAL REQ` badge (Catalogue) have no backing anywhere. | NOT SPECIFIED |
| **K6** | **Team member identity.** Ownership is assigned to labels **M1–M5**, never to names. Who is M1? | NOT SPECIFIED |
| ~~K7~~ | ~~**.NET version.**~~ **Resolved 2026-08-24.** The team confirmed **.NET 10**; the Plan was revised to **v1.1** (§0 revision history, §1.2.4 rewritten, §1.1/§2.2/§7 M0 aligned) and `__ai_agents/backend.md` updated to match. `.csproj` × 4 and `README.md` already agreed. Every machine now needs the **.NET 10 SDK** — a .NET 8 SDK fails `NETSDK1045` and cannot read `Project.slnx` (`MSB4068`). | ✅ Closed |
| ~~K8~~ | ~~**Auth: Identity framework vs. Plan's custom auth.**~~ **Resolved 2026-08-27.** Plan §T1.2/line 850 specifies a hand-rolled design: `Core/Entities/{User,Role,RoleThreshold}.cs`, `Core/Interfaces/{IUserRepository,IPasswordHasher,ITokenService}.cs`, plain `PasswordHasher<T>` (line 217) — **not** the full ASP.NET Core Identity framework. `docs/development/identity-and-user-management-implementation-plan.md` proposes `IdentityDbContext<ApplicationUser,ApplicationRole,int>` + `UserManager`/`RoleManager`/`SignInManager` instead. User confirmed 2026-08-27: **keep Identity**, overriding the Plan's custom design. Consequence not yet reconciled: Identity's generated tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`) replace the single `Users` table in the 12-table ERD and `StationerySchema.sql:33-48` — the ERD, `Roles` FK, and any table referencing `Users.EmployeeNumber` need updating to match before the identity migration is announced. Record this override in the Plan itself (revision history) the next time the Plan is touched, the same way K7 was closed. | ✅ Closed — Identity kept |

Twelve further ambiguities are already tracked by the team as `[ASK]` #1–12 in **Plan §14**, each
with a default implemented behind a flag. Read that list before asking a new question.

---

## 7. Documentation References

**Authority order** — the team's source of truth is `__ai_agents/`, not `docs/`.

| Rank | Path | What it is |
|---|---|---|
| 1 | [__ai_agents/Stationery_Management_System_Project_Plan.md](__ai_agents/Stationery_Management_System_Project_Plan.md) | **The Plan.** v1.0, *"Baseline — approved for execution"*. Architecture §2 · schema + state machine §3 · **full ~45-endpoint catalogue §4** · AI design §5 · ownership §6 · milestones M0–M7 §7 · git §8 · coding standards §9 · testing + TC-01…TC-25 §10 · risks §11 · deployment §12 · deliverables §13 · `[ASK]` §14 |
| 2 | [docs/web based Stationery Management System.docx](docs/web%20based%20Stationery%20Management%20System.docx) | Original Aptech requirements — what `[SPEC]` refers back to |
| 3 | [__ai_agents/backend.md](__ai_agents/backend.md) · [frontend.md](__ai_agents/frontend.md) | Quick-reference summaries. Both say: *if it and the Plan disagree, the Plan wins* |
| 4 | [__ai_agents/Database/StationerySchema.sql](__ai_agents/Database/StationerySchema.sql) | T-SQL DDL, 12 tables. "Schema of record" per `systemprompt.md` — **but see K2/K3** |
| 5 | [docs/Diagrams/](docs/Diagrams) | ERD (12 tables) · DFD L0–L2 · request state machine + flows · UML activity |
| 6 | [docs/Wireframe/](docs/Wireframe) | Dashboard · Catalogue · Request · Approvals · Inventory (5 only) |
| — | [AI_usage_report.md](AI_usage_report.md) | Root-level AI usage log. **Append, never overwrite** (`systemprompt.md`) |
| — | [docs/development/](docs/development) | Reconciliation notes and handoffs: architecture gaps, coding rules, page map, and the [Identity/user-management implementation plan](docs/development/identity-and-user-management-implementation-plan.md) (the design doc behind the auth work landed 2026-08-27/28 — see §1) |

**Superseded:** `__ai_agents/Database/Project 3.sql` — an earlier draft with a *different* domain
model (Department / Storage / SubCategory / StationeryRequestPasson). Kept only for diffing.
`README.md` — stale .NET version.

**Referenced but absent from the repo:** `__ai_agents/Requirements/` (per-feature specs, cited by
`systemprompt.md`), `docs/GUI-Standards.md`, `docs/AI-Usage-Report.md` (the Plan wants it at that
path; the actual log is at root `AI_usage_report.md`), `docs/rollback/`, `docs/postman/`,
`docs/releases/`, and the Plan's own cited sources (`Phân chia công việc.xlsx`,
`Stationery_Management_System_Roadmap.md`, `Startup_Product_Kickoff.pdf`, `cau_truc_du_an.md`,
`cau_hinh_funnel_tailscale.md` — the last two are in git history).

---

## 8. Rules for AI-Assisted Development

- **Plan before implementing.** State approach, affected layers and trade-offs first. When the
  request is ambiguous, **state the assumption and flag it** — never guess quietly
  (`systemprompt.md`).
- Check `__ai_agents/Requirements/` for a feature's spec before assuming scope; **it does not
  exist yet**, so say so rather than inferring silently.
- Treat `__ai_agents/Database/*.sql` as the data model — subject to K2/K3.
- **Log AI work**: after writing or changing code, **append** a dated entry to
  `AI_usage_report.md` at the repo root — task in one line, what changed by file, assumptions
  made, what was left out of scope. Never overwrite; never fabricate.
- Every task report states: what was implemented · files changed · APIs added/changed · DB
  changes · **tests actually executed** · wireframe fidelity · shared files touched · TODOs.
- **Completion documentation is mandatory for AI-assisted work.** After finishing implementation,
  append a dated, truthful entry to root `AI_usage_report.md` (never overwrite it) and create or
  update a task-specific Markdown handoff under `docs/development/`. The handoff must explain the
  architecture and flow, files changed, APIs and DB changes, setup and usage, tests actually run,
  assumptions, exclusions, known issues, and reviewer follow-ups in enough detail for another team
  member to understand and explain the implementation.
- The owning developer reviews and must be able to explain the code. Reviewers ask
  *"explain this"* in the PR — that is the process, not an insult.
