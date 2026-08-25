# Architecture — Reconciliation Notes

> Synchronised with `origin/main` @ **95b4553**, 2026-08-24.
>
> **This is not the architecture document.** That is
> [the Plan](../../__ai_agents/Stationery_Management_System_Project_Plan.md) §2–§5, §9, §12.
> This file records only what the Plan does *not* say: the gap between the documented design and
> the actual repository, and the conflicts between documents. Read the Plan for the design.

---

## 1. Where each architectural question is answered

| Question | Answer lives in |
|---|---|
| Style, component view, dependency rule, rejected patterns | Plan **§2.1–§2.4** |
| Cross-cutting concerns (authN, authZ, validation, errors, logging, time, concurrency, transactions, secrets) | Plan **§2.5** |
| Spec-derived DB constraints, ERD, table reference, header/line split, stock ledger, **state machine**, indexes, seed data | Plan **§3.1–§3.8** |
| API conventions, **the full ~45-endpoint catalogue**, error contract | Plan **§4.1–§4.3** |
| AI feature design (A1/A2/A3), prompt-injection defence, fallback, logging | Plan **§5** |
| Coding standards (C#, React, database) | Plan **§9** |
| Testing strategy, tooling, TC-01…TC-25 | Plan **§10** |
| Deployment pipeline and checklist | Plan **§12** |
| Backend / frontend quick reference | [`__ai_agents/backend.md`](../../__ai_agents/backend.md) · [`frontend.md`](../../__ai_agents/frontend.md) |

Both quick-reference files state explicitly: **if they and the Plan disagree, the Plan wins.**

---

## 2. Documented design vs. actual repository

Nothing in the Plan's architecture is implemented. Current state, verified 2026-08-24:

| Area | Plan says | Repo actually has |
|---|---|---|
| Runtime | **.NET 10** `[DECISION — Plan §1.2.4, v1.1]` | `.csproj` × 4 target **`net10.0`** ✅. But the installed SDK is **8.0.422** → `dotnet build` fails **`NETSDK1045`**, and `Project.slnx` fails **`MSB4068`** (`.slnx` needs a .NET 9+ SDK). **Install the .NET 10 SDK** — nothing to fix in the project files |
| Solution | `Project.slnx` (used by Dockerfile + Jenkinsfile) | ✅ restored 2026-08-24 |
| `Core` | Entities, enums, domain exceptions, repository interfaces | generic `IRepository<T>` only (`Class1.cs` deleted 2026-08-24) |
| `Application` | Services, `record` DTOs, FluentValidation validators | a pass-through generic `Service<T>` (**not registered in DI**) |
| `Infrastructure` | `AppDbContext`, EF configs, migrations, `JwtTokenService`, `PasswordHasher`, `LlmClient` | `DataContext` with **no `DbSet`**, generic `Repository<T>`. No provider package, no connection string, no migration |
| `WebApi` | Controllers, `ExceptionHandlingMiddleware`, Serilog, Swagger + JWT | **no controllers at all** (`WeatherForecast*` deleted 2026-08-24); `AddDbContext` with **no provider** (throws at runtime); `UseAuthorization()` with **no `UseAuthentication()`** |
| `frontend` | React 18 + Tailwind + React Router + axios + `AuthContext` | stock Vite vanilla-JS counter demo; no framework, no router, no `vite.config.*`, no `node_modules` |
| Repo hygiene | `.gitignore`, `.editorconfig`, CODEOWNERS, PR template, branch protection | `.gitignore` is the **Node template only** — `bin/` and `obj/` are committed |

All of this is scheduled as Plan **M0 tracks T0.1–T0.6**. None of it is done.

### The one architectural gap the Plan closes that the old code contradicts

The existing `Repository<T>` calls `SaveChangesAsync()` **inside every write method**, so every
write is its own transaction — incompatible with the Plan's requirement that approval decrement
stock, write the ledger, set status, write history and insert two notifications atomically.

The Plan's answer (§2.4) is explicit: **do not add a `UnitOfWork` wrapper.** `DbContext` *is* the
unit of work — call `SaveChangesAsync` inside the service, and wrap multi-step transitions in
`IDbContextTransaction` with `IsolationLevel.ReadCommitted` (§7 M4/T4.2). The generic repository
stays for simple by-id/CRUD access only.

---

## 3. Open conflicts between documents

Detail and current status: [CLAUDE.md §6](../../CLAUDE.md). Summary:

- **K1 — `ReturnedForModification`.** Plan §3.6/§4.2/T4.3 and `frontend.md` include a
  return-for-modification branch; `docs/Diagrams/request_diagrams_v3.drawio` declares it out of
  scope by team decision and says the Plan must be updated to match. **Blocking** the request
  status enum, `POST /requests/{id}/return`, and whether an approver has two outcomes or three.
- **K2/K3 — `StationerySchema.sql` vs Plan §3.1/§3.6.** The SQL uses `IDENTITY(1,1)` with no
  `CHECK 1–1000`, `NVARCHAR(200)`/`NVARCHAR(256)` instead of `nvarchar(15)`/`nvarchar(25)`, and
  `CHECK` constraints containing `PartiallyApproved`, `Fulfilled`, `Inbound`, `Outbound`,
  `Return` — values that appear in no other document. `AI_usage_report.md` flags all of these as
  assumptions. The Plan's versions are `[SPEC]`-derived → **the Plan wins; the SQL needs fixing.**
- **K4 — migrations vs raw SQL.** Plan §9.3 says EF migrations only, never hand-edit the DB;
  `systemprompt.md` calls the `.sql` files the schema of record. Unreconciled.
- **K5 — wireframe fields with no backing:** `Department`, `Notify Me`, `MGR APPROVAL REQ`.
  `SKU` is listed in the Plan as a *future improvement* → do not build it.

The diagrams in `docs/Diagrams/` remain valid on everything except K1 — the DFD's L2 process
decompositions, the transaction boundaries and the error-code choices all match the Plan.
`docs/Diagrams/approval_transaction.drawio` is still the odd one out: it models withdraw as a row
`DELETE`, which both the Plan (§3.6) and `request_diagrams_v3` forbid.

---

## 4. Decisions of record

The Plan's `[DECISION]` labels are the register — don't maintain a second copy here. The ones
most likely to be accidentally violated during a page task:

| Plan § | Decision |
|---|---|
| §1.2.4 | **.NET 10** — revised in Plan **v1.1** (2026-08-24), reversing v1.0's `[DECISION] .NET 8 (LTS)`. `backend.md` updated to match |
| §2.1 | Modular monolith, single container — no microservices |
| §2.4 | No MediatR/CQRS · no `UnitOfWork` · no AutoMapper · no SignalR · no blanket soft delete |
| §2.4 | `IRepository<T>` for simple CRUD only; named query interfaces for reports |
| §3.1 | Superior `0` stored as **`NULL`**, mapped `0 ↔ NULL` at the API/import boundary |
| §3.4 | Header/line request split; `UnitCostSnapshot` frozen at submission |
| §3.5 | Stock is an append-only ledger; `QuantityAvailable` is a cached balance |
| §3.6 | Only `RequestStateMachine.Transition()` writes `Request.Status`; never `DELETE` a request |
| §4.1 | `/api/v1/...` from day one; camelCase JSON, kebab-case URLs; DTOs only, never EF entities |
| §4.3 | Not yours → **404, not 403** |
| §5.2 | The LLM never writes to the database; user text is a `user` message, never concatenated into the system prompt; offline fallback is mandatory |
| §9.2 | JWT in `localStorage` — a *knowing* trade-off, not an oversight. Don't "fix" it unilaterally |
| §12.2 | Swagger stays enabled (academic demo), noted in the report as production-gated |
| §12.3 | Migrations run by **explicit script**, not on startup, so a bad migration can't take the app down on boot |

---

## 5. Still NOT SPECIFIED

- **Who M1–M5 actually are** — ownership is assigned to labels, never to names.
- `__ai_agents/Requirements/` — cited by `systemprompt.md` as the per-feature spec location;
  **does not exist**. Say so rather than inferring a feature's scope.
- `docs/GUI-Standards.md` — a `[SPEC deliverable]` (Plan §9.2, §13 #8) holding the Tailwind design
  tokens; not written. The wireframes are the only visual reference.
- The AI **provider and model** (`AiInteractionLogs.ModelName` exists; nothing names the vendor).
- Currency — Plan §14 `[ASK]` #10 defaults to **VND**, `decimal(18,2)`; the wireframes show `$`.
- Whether the two mandatory status emails and the 13 eProject documents (Plan §13) have started.

Twelve further ambiguities are already tracked with implemented defaults in **Plan §14** —
check there before raising a new question.
