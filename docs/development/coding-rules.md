# Coding Rules — Working Checklist

> Synchronised with `origin/main` @ **95b4553**, 2026-08-24.
>
> **The authoritative standards are** [the Plan](../../__ai_agents/Stationery_Management_System_Project_Plan.md)
> **§8 (Git), §9 (Coding Standards) and §10 (Testing)**, summarised in
> [`__ai_agents/backend.md`](../../__ai_agents/backend.md) and
> [`frontend.md`](../../__ai_agents/frontend.md).
>
> This file is the day-to-day checklist: the rules that get broken most often, plus the things
> the Plan leaves to judgment. Where this file and the Plan differ, **the Plan wins**.

---

## 1. Before you write anything

- [ ] Which **milestone and track** is this? (`M3 / T3.4`) — PRs must link it.
- [ ] Read the relevant **Plan section**. Quote the label when justifying a choice:
      `[DECISION — Plan §2.4]`.
- [ ] Is it on the **`[CUT]` list** (Plan §1.3)? Building a cut item is a scope breach.
- [ ] Does the file you're about to modify **actually exist**? Almost nothing does — the repo is
      pre-M0 scaffolding. If you're first, you're setting the precedent; **say so in the report**.
- [ ] Ambiguous? **State the assumption and flag it.** Never guess quietly (`systemprompt.md`).

---

## 2. Naming

Standard .NET — Plan §9.1. `PascalCase` types/methods · `camelCase` locals/params ·
`_camelCase` private fields · `I`-prefixed interfaces · every I/O method `async` + `Async`
suffix · one public type per file · folder = namespace.

**Use the schema's names exactly** — `Users`, `StationeryItems`, `RequestItems`,
`StockTransactions`, `AiInteractionLogs`, `EmployeeNumber`, `UnitCostSnapshot`,
`MinRankLevelToRequest`. C# class singular (`User`), `DbSet` plural (`Users`).

**`RequestStatus` and `NotificationEventType` are enums — no magic strings** (Plan §9.1).

⚠️ The **exact status set is contested** — see K1 in [CLAUDE.md §6](../../CLAUDE.md). Ask before
writing the enum. Also do **not** copy the `CHECK` constraint values out of
`StationerySchema.sql`: `PartiallyApproved`, `Fulfilled`, `Inbound`, `Outbound` and `Return`
appear in no other document and are flagged as guesses in `AI_usage_report.md`.

**Branches:** `<type>/<milestone>-<kebab-description>`, lower-case, no personal names, ≤50 chars.
`feat/` `fix/` `chore/` `docs/` `test/` `refactor/`.

---

## 3. Layering — the rule reviewers must enforce

`Core` → nothing · `Application` → `Core` · `Infrastructure` → `Core` · `WebApi` → both.

**If `using Microsoft.EntityFrameworkCore;` appears in `Core` or `Application`, reject the PR.**

| Layer | Contains | Never contains |
|---|---|---|
| `Core` | Entities, enums, domain exceptions, repository *interfaces* | EF Core, ASP.NET types, DTOs, HTTP |
| `Application` | Services, `record` DTOs, FluentValidation validators, AI orchestration interfaces | `DbContext`, `HttpContext`, SQL |
| `Infrastructure` | `AppDbContext`, EF configs, migrations, repositories, `JwtTokenService`, `PasswordHasher`, `LlmClient` | Business rules |
| `WebApi` | Controllers, middleware, DI, Swagger, SPA fallback | Business rules, direct SQL |

---

## 4. Services and controllers

**Controllers are thin:** model-bind → call service → map to `ActionResult`.
**Zero business logic, zero `try/catch`** (one `ExceptionHandlingMiddleware` maps domain
exceptions to RFC 7807 `ProblemDetails`), zero direct `DbContext`.

**Services** own the business rules. One responsibility, constructor injection, no static mutable
state, DTOs in and out — **entities never leave the Application layer**.

Two traps specific to this codebase:

1. **Don't add a `UnitOfWork`** (Plan §2.4). `DbContext` *is* the unit of work. Call
   `SaveChangesAsync` inside the service; wrap multi-step transitions in `IDbContextTransaction`.
   Note the existing generic `Repository<T>` saves on **every** write — it is for simple
   by-id/CRUD only, not for transactional workflows.
2. **Don't push reports through `IRepository<T>`.** Use named query interfaces
   (`IReportQueries.GetCostByItemAsync()`) implemented in Infrastructure, with SQL-side
   `GROUP BY`. `ToListAsync()` then LINQ-in-memory is the mistake examiners look for.

---

## 5. API

Plan §4.1 / §4.3 — the full endpoint catalogue is Plan §4.2, **use it rather than inventing routes**.

- Base path `/api/v1/...`; camelCase JSON, kebab-case URLs, plural nouns.
- `Authorization: Bearer <jwt>` on everything except `/api/v1/auth/login` and `/health`.
- Paging `?page=1&pageSize=20` → `{ items, page, pageSize, totalCount }`.
- **Never return an EF entity** — lazy-loading cycles and accidental `PasswordHash` leakage.
- Never accept a server-authoritative field from the client: `Status`, `UnitCostSnapshot`,
  `TotalEstimatedCost`, `ApproverEmployeeNumber`, `DecidedAtUtc`. Take the actor from the token.

| Code | When |
|---|---|
| 400 | Validation failure (name contains `_`; quantity ≤ 0; missing mandatory comment) |
| 401 | Missing / expired token |
| 403 | Authenticated but not permitted (Engineer opens `/reports/*`) |
| 404 | Not found **or found but not yours** — do not leak existence |
| 409 | State conflict; stale `RowVersion` |
| 422 | Business rule violation (over role threshold — **name the limit and the overage**) |
| 503 | LLM provider down — AI endpoints degrade, never crash |

---

## 6. Entity Framework

- **Migrations only. Never hand-edit the database.** One logical change per migration, named
  `20260810_AddRequestStatusHistory`. **Only one open PR may contain a migration at a time** —
  announce it in team chat. M3 is the migration custodian.
- Configure with `IEntityTypeConfiguration<T>` in Infrastructure — `Core` must not see EF.
- `decimal(18,2)` for money, never `float`/`double`. `datetime2`, **always UTC**, suffixed `Utc`.
- Constraint prefixes `PK_` `FK_` `IX_` `UQ_`; tables and columns `PascalCase` plural.
- **No `ON DELETE CASCADE`** on transactional data — use `Restrict` and handle it in the service.
- `RowVersion` on `Requests` and `StationeryItems`; catch `DbUpdateConcurrencyException` → **409**.
- **Never hard-delete** a `User`, `StationeryItem` or `Supplier` (`IsActive = false`), a ledger
  row, a history row, or a submitted `Request`. `Draft` is the only deletable request state.
- Read-only queries `AsNoTracking()`. Filter, aggregate and page in SQL. Watch `Include` for N+1.
- Generate and commit the down script per milestone (`docs/rollback/MX_down.sql`) **before**
  merging, not under pressure afterwards.

---

## 7. Frontend

React 18 + Vite + Tailwind + React Router — Plan §9.2. The current `frontend/` is a bare Vite JS
scaffold; **it is not the target state**, and converting it is Plan track T0.5.

- Function components + hooks only. One per file, `PascalCase.jsx`. Over ~150 lines or 3 levels of
  JSX nesting → extract.
- **Every data-fetching component needs loading, error and empty states.** A happy-path-only
  component is incomplete and will be sent back in review.
- All API calls through `src/api/*.js` on the shared axios instance with the JWT interceptor.
  **No `fetch` scattered through components.**
- Local `useState` first; `AuthContext` for the session. **No Redux.**
- Tailwind utilities in markup; extract repeated clusters into a component, not `@apply` soup.
  No inline `style={{}}`. Design tokens live in `tailwind.config.js` + `docs/GUI-Standards.md`.
- Controlled inputs; validate client-side for UX, **trust only the server**.
- Accessibility: labels on inputs, keyboard-reachable buttons, visible focus ring, ≥4.5:1 contrast.
- **Nothing sensitive in `import.meta.env`** — anything shipped to the browser is public.
- JWT in `localStorage` is a **deliberate, documented** trade-off (Plan §9.2). Don't "fix" it
  unilaterally; do be able to explain the XSS risk and that `httpOnly` cookies + CSRF is the
  production answer.

---

## 8. Validation, auth and security

- **Server-side always.** FluentValidation in Application, one validator per DTO.
  `ValidationException` → 400 via middleware. UI checks are UX, not a control.
- **Authorisation in two places, always:** a policy on the controller (`RequireManager`,
  `RequireApprover`) **plus** a row-level ownership check inside the service — attributes don't
  know *which* request is being acted on. An examiner will curl the endpoint directly.
- Spec field rules `[SPEC]`: `EmployeeNumber` 1–1000 (and it is the login) · `Name` ≤ 15 chars,
  no underscores/special characters · `EmailId` ≤ 25 chars, unique.
  ⚠️ `StationerySchema.sql` currently violates all three — see K2.
- Passwords: `PasswordHasher<T>` (PBKDF2, per-user salt, 100k iterations). Never plain text,
  never logged, never in a DTO, never returned.
- Secrets: `appsettings.json` for non-secrets only. **Connection strings and the LLM API key via
  environment variables only.** `.env` in `.gitignore`. A key in git history is a real incident.
- **Never weaken a check to make development easier.** If it blocks you, it is working.

---

## 9. Testing

Plan §10. **Not a coverage exercise** — every business rule in §3.6 and §5 needs a named test
whose failure means the requirement is broken.

- Unit: **xUnit + FluentAssertions + Moq**.
- Integration: `WebApplicationFactory<Program>` + EF Core **SQLite in-memory** — *not* the
  `InMemory` provider, which enforces neither FKs nor transactions and will pass tests that
  production fails.
- Frontend: Vitest + React Testing Library, ~5 critical components (timeboxed).
- **TC-01…TC-25 in Plan §10.3 are mandatory** and map to spec requirements. The highest-value
  ones: TC-08 approval rollback · TC-13 six triggers × two recipients · TC-16 percentages sum to
  100.00% · TC-23 AI fallback · TC-25 concurrency.
- Target ≥60% coverage on `Application`, **with every §3.6 rule covered regardless of percentage**.
- **Report only tests you actually executed.** ⚠️ The repo does not currently build on this
  machine: the projects target `net10.0` (correct) but only SDK **8.0.422** is installed, so
  `dotnet build` fails `NETSDK1045` and `Project.slnx` fails `MSB4068`. **Installing the .NET 10
  SDK is the unblock** — don't promise build results until then.

---

## 10. Git and PRs

Trunk-based, one protected `main`, **no `develop`** (Plan §8.1).

- Branch life **max 2 days**. Rebase on `main` before opening the PR. **Squash merge.**
- **PRs under 400 lines** excluding generated files and migrations. Larger PRs get rubber-stamped.
- Conventional commits: `feat(requests): block submission over role threshold`.
- **Self-merging is prohibited**, including for the Project Leader. Review within 4 working
  hours; **2 reviewers** for auth, workflow and stock changes.
- Complete the PR template: no secrets · no `Console.WriteLine`/`TODO`/commented-out code · DTOs
  not entities · authorisation server-side · `AI_usage_report.md` updated · migration announced.
- **AI agents never commit, push, merge, rebase, force-push or create branches unless explicitly
  instructed.**

**A reviewer checks** correctness against the claimed acceptance criterion, server-side and
ownership-aware authorisation, layering, data safety (hard deletes, unbounded queries, missing
transactions), whether a new business rule has a test that would fail without the change — and,
if AI wrote it, **asks the author to explain it**.

---

## 11. Logging AI work

After writing or changing code, **append** (never overwrite) a dated entry to
[`AI_usage_report.md`](../../AI_usage_report.md) at the repo root: the task in one line, what
changed by file, assumptions made where the request was ambiguous, and what was deliberately left
out of scope. Distinguish the AI's contribution from the developer's. Never fabricate prompts,
dates, results or tests.

> Note: the Plan §5.4 and §13 refer to this file as `docs/AI-Usage-Report.md`. The file that
> actually exists — and that `systemprompt.md` points every agent at — is `AI_usage_report.md`
> at the repo root. Use the one that exists; don't start a second log.
