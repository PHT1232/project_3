# Backend Context

Source of truth for architecture/process decisions: `__ai_agents/Stationery_Management_System_Project_Plan.md` (the Plan). Schema of record: `__ai_agents/Database/*.sql`. This file is a quick-reference summary — if it and the Plan disagree, the Plan wins.

## Architecture
Clean Architecture, modular monolith, single deployable container. **[DECISION — Plan §2.1]** No microservices: a 3-week, 5-person project has no scaling or team-boundary problem that justifies the operational cost.

1. **Core**: Domain layer. Entities, enums, domain exceptions, repository *interfaces*. **No dependencies** — never EF Core, never ASP.NET types, never DTOs/HTTP.
2. **Application**: Business logic layer. Services, DTOs (`record` types), FluentValidation validators, AI orchestration interfaces. *Depends on Core.* Never references `DbContext`, `HttpContext`, or raw SQL.
3. **Infrastructure**: Data access layer. `AppDbContext`, EF Core configurations & migrations, repository implementations, `JwtTokenService`, `PasswordHasher`, `LlmClient`. *Depends on Core.*
4. **WebApi**: Presentation layer (ASP.NET Core API). Controllers, middleware, DI setup, Swagger. *Depends on Application and Infrastructure.* Controllers are thin: model-bind → call service → map to `ActionResult`. Zero business logic, zero `try/catch` (a single `ExceptionHandlingMiddleware` maps domain exceptions to RFC 7807 `ProblemDetails`), zero direct `DbContext` use.

Dependencies point inwards, always (Plan §2.3). If `using Microsoft.EntityFrameworkCore;` appears in `Core` or `Application`, the abstraction has leaked — reject the PR.

## Tech Stack & Patterns
- **Language**: C#
- **Framework**: **.NET 10** — `[DECISION — Plan §1.2.4, revised in plan v1.1]`. The `.csproj` files and `README.md` already agree; there is no drift to fix. (Plan v1.0 called for .NET 8 LTS and a downgrade — that decision was reversed on 24 Aug 2026.) **Every machine needs the .NET 10 SDK**: a .NET 8 SDK fails with `NETSDK1045` and additionally cannot read `Project.slnx` (`MSB4068`).
- **Database/ORM**: Entity Framework Core, **SQL Server 2022**. Migrations only — never hand-edit the database.
- **Patterns**: Dependency Injection, Repository Pattern (generic `IRepository<T>` for simple by-id/CRUD only), Generic Services, plain injected services (constructor injection, one responsibility, no static mutable state).
- **Explicitly rejected** (Plan §2.4) — do not introduce these: MediatR/CQRS, a generic `UnitOfWork` wrapper over `DbContext` (call `SaveChangesAsync` inside the service — `DbContext` *is* the unit of work), AutoMapper (write explicit `ToDto()` extension methods instead), SignalR (poll `GET /api/v1/notifications/unread-count` instead), soft delete on every table (only `Users`, `StationeryItems`, `Suppliers` get `IsActive`).
- **Reports/joins**: never force `GROUP BY`/aggregation queries through the generic repository — use explicit named query interfaces (e.g. `IReportQueries.GetCostByItemAsync()`) implemented in Infrastructure.
- **Integration**: The WebApi serves the frontend SPA using `UseStaticFiles()` and `MapFallbackToFile("index.html")`.

## Domain model (see schema for full detail)
Header/line request model: `Requests` (header: requestor, approver, status, total) + `RequestItems` (lines, with `UnitCostSnapshot` frozen at submission so later price edits never rewrite history). Stock is a **ledger, not a counter**: `StationeryItems.QuantityAvailable` is a cached balance; `StockTransactions` is the append-only source of truth, and every balance change must write a matching ledger row in the same DB transaction. Self-referencing `Users.SuperiorEmployeeNumber` hierarchy (Engineer → Manager → Business Manager → MD); top of hierarchy is `NULL`, not `0` (spec's `0` is mapped to `NULL` at the API/import boundary).

Request state machine (Plan §3.6) — implement as a single guarded transition method, never scattered `if` statements: `Draft → Pending → {Approved | Rejected | ReturnedForModification | Withdrawn}`, `Approved → CancellationPending → {Cancelled | Approved}`. Approve/Cancel must update status, adjust stock, and insert notifications in **one DB transaction**. Never `DELETE` a request — status transitions only. Withdraw (unilateral, from `Pending`) and Cancel (two-step, from `Approved`, requires superior sign-off) are distinct operations — do not conflate them.

## Cross-cutting concerns (Plan §2.5)
| Concern | Approach |
|---|---|
| AuthN | JWT bearer, HS256, 8-hour expiry, `sub` = EmployeeNumber |
| AuthZ | ASP.NET policies (`RequireManager`, `RequireApprover`) **plus** row-level ownership checks inside every service method |
| Validation | FluentValidation in Application; `ValidationException` → HTTP 400 via middleware |
| Error handling | Single `ExceptionHandlingMiddleware` → RFC 7807 `ProblemDetails`; never a bare 500 with a stack trace |
| Logging | `ILogger<T>` + Serilog to console (Docker-friendly) and rolling file |
| Time | All timestamps stored **UTC**, converted at the UI boundary — non-negotiable |
| Concurrency | `byte[] RowVersion` on `Requests` and `StationeryItems`; catch `DbUpdateConcurrencyException` → HTTP 409 |
| Config/secrets | `appsettings.json` for non-secrets only; connection strings and the LLM API key via **environment variables only** |

## API conventions (Plan §4.1)
Base path `/api/v1/...`. camelCase JSON, kebab-case URLs, plural nouns. `Authorization: Bearer <jwt>` required except `/auth/login` and `/health`. Controllers never return EF entities (DTOs only). Paging: `?page=1&pageSize=20` → `{ items, page, pageSize, totalCount }`. State-transition endpoints return 409 if the entity isn't in the expected state. Full endpoint catalogue and error contract (400/401/403/404/409/422/503) are in Plan §4.

## AI feature (Plan §5)
Server-side only via `LlmClient` in Infrastructure; the API key never reaches the frontend. The LLM never writes to the database — it returns a proposal that a service validates against the real catalogue/thresholds before the user reviews and submits it. User text goes in as an untrusted `user` message, never concatenated into the system prompt. 10s timeout + one retry, then a deterministic fallback — the demo must work with the network unplugged. Every AI call is logged to `AiInteractionLogs` (feature, model, latency, `WasFallback`) — this table is the AI-Usage-Report evidence.

## Coding standards (Plan §9.1)
`PascalCase` types/methods, `camelCase` locals/params, `_camelCase` private fields, `I`-prefixed interfaces. Every I/O method is `async`, suffixed `Async`; never `.Result`/`.Wait()`; pass `CancellationToken` through. `<Nullable>enable</Nullable>` in every `.csproj`. `RequestStatus` and `NotificationEventType` are enums — no magic strings. Domain exceptions live in `Core/Exceptions`. XML doc comments (`///`) on every public class/method; inline comments only to explain *why* on non-obvious logic. One public type per file.

## Testing (Plan §10)
xUnit + FluentAssertions + Moq for unit tests (state machine, eligibility, reports, AI validation). Integration tests via `WebApplicationFactory<Program>` + EF Core **SQLite in-memory** (not the EF `InMemory` provider — it doesn't enforce FKs/transactions). See Plan §10.3 for the mandatory test-case list (TC-01…TC-25) mapped to spec requirements.
