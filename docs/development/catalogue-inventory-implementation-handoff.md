# M2 — Catalogue, Suppliers & Stock Ledger: Implementation Handoff

> Implemented 2026-08-28 against `main`, executing
> `docs/development/m2-catalogue-suppliers-stock-implementation-plan.md` end to end. Full
> file-by-file detail is in the dated `AI_usage_report.md` entry
> ("Implement M2: Catalogue, Suppliers & Stock Ledger") — this doc is the architecture/flow
> summary for a reviewer picking this up cold.

## Architecture as built

Matches the plan's §1–§9 Clean Architecture split, with the plan-review fixes applied (see the
plan doc's own inline notes) plus a few more found necessary during implementation:

- **`Guid RowVersion`, not SQL Server's native `rowversion`.** Configured with
  `.IsConcurrencyToken()` (not `.IsRowVersion()`) on `Supplier` and `StationeryItem`. Concurrency
  is enforced as a manual compare-then-set in `ItemService.UpdateItemAsync`,
  `SupplierService.UpdateSupplierAsync`, and `StockService.ApplyAsync` — not EF's
  `DbUpdateConcurrencyException` path — so behavior is identical against SQL Server (production)
  and SQLite (integration tests). Every mutable response DTO (`ItemDto`, `SupplierDto`,
  `InventoryRowDto`) carries the current `RowVersion`; every mutating request DTO
  (`UpdateItemRequest`, `UpdateSupplierRequest`, `AdjustStockRequest`, `ReceiveGoodsRequest`)
  requires the client to send back the version it last read.
- **`StockTransactions.CreatedByEmployeeNumber` FKs to `ApplicationUser` (`AspNetUsers`)**, not
  the legacy `Users` table from `StationerySchema.sql` — confirmed in the generated migration,
  not just assumed (K8).
- **The single write path onto the stock ledger is `IStockService`** (`Infrastructure/Services/
  StockService.cs`): loads the item, checks the RowVersion, checks the resulting balance won't go
  negative, updates `QuantityAvailable`, writes the `StockTransaction` row, and calls
  `SaveChangesAsync()` once — all in one method, matching CLAUDE.md's ledger-consistency rule.

## APIs added

| Method | Route | Auth |
|---|---|---|
| GET | `/api/v1/categories` | Any authenticated |
| GET | `/api/v1/items` | Any authenticated (role-filtered) |
| GET | `/api/v1/items/{id}` | Any authenticated (role-filtered) |
| POST/PUT | `/api/v1/items[/{id}]` | Manager+ |
| PATCH | `/api/v1/items/{id}/deactivate` | Manager+ |
| POST/PUT | `/api/v1/categories[/{id}]` | Manager+ |
| PATCH | `/api/v1/categories/{id}/deactivate` | Manager+ |
| GET/POST/PUT | `/api/v1/suppliers[/{id}]` | Manager+ |
| PATCH | `/api/v1/suppliers/{id}/deactivate` | Manager+ (409 if active items reference it) |
| GET | `/api/v1/inventory`, `/api/v1/inventory/low-stock` | Manager+ |
| POST | `/api/v1/inventory/{itemId}/adjust`, `/receive` | Manager+ |
| GET | `/api/v1/inventory/{itemId}/transactions` | Manager+ |

## DB changes

One migration, `CatalogueSuppliersAndStock`: `Categories`, `Suppliers`, `StationeryItems`,
`StockTransactions`. Check constraints `CK_StationeryItems_MinRankLevelToRequest` (1–4) and
`CK_StockTransactions_ChangeQuantity` (`<> 0`).

**✅ Applied to a real SQL Server instance on 2026-08-28** — SQL Server 2022 Express
(`.\SQLEXPRESS`, 16.0.1000.6), database `StationeryManagementSystem.Dev`. Both migrations ran
clean; all 12 tables and all 3 check constraints were created as designed. This supersedes the
earlier "not applied to a real SQL Server instance" note. The connection string was supplied as
the `ConnectionStrings__DefaultConnection` environment variable — `appsettings.Development.json`
still points at LocalDB, which is not installed on that machine (see follow-up 6).

## Setup and usage

1. .NET 10 SDK, Node 20+, and a SQL Server instance reachable via
   `ConnectionStrings:DefaultConnection` (same requirement as M1).
2. Set `Jwt:SigningKey` and `Seed:BootstrapAdminPassword` via environment variables in every
   non-development environment (`Jwt__SigningKey`, `Seed__BootstrapAdminPassword`) — both ship
   insecure dev-only placeholders in `appsettings.Development.json`.
3. `dotnet run --project WebApi` — applies migrations, seeds the 4 roles, seeds one bootstrap
   Managing Director account (employee #1), then seeds 5 categories / 6 suppliers / 40 items /
   ~90 days of stock history if the catalogue tables are empty. All idempotent.
4. Sign in as employee #1 with the configured bootstrap password to create real Manager+ users,
   or use the seeded catalogue data directly.
5. Frontend: `cd frontend && npm install && npm run dev`. New pages: `/catalogue/manage` (Item
   Management), `/suppliers` (now real, replacing the placeholder), `/inventory` (now wired to
   the real API, previously mock-backed).

## Tests actually run

- `dotnet test Project.slnx`: **49/49 passed** (26 `Application.UnitTests`, 23
  `WebApi.IntegrationTests` against `WebApplicationFactory<Program>` + EF Core SQLite in-memory).
- `npx vitest run` (frontend): **22/22 passed**.
- `npm run build` and `dotnet build Project.slnx`: both succeed.
- **Not done:** a live SQL Server migration run, or a manual browser click-through. Treat the UI
  as code-reviewed and unit/integration-tested, not as visually verified.

## Assumptions carried into the build

- `[ASK]` #3 (role filter): implemented as `MinRankLevelToRequest <= caller.RankLevel`, the
  plan's stated default — not confirmed with the instructor.
- `[ASK]` #3 supplier lead-time unit: days (integer), as assumed in the plan.
- Inventory status thresholds (`REORDER_NOW`/`WATCH`/`OK`) are a simple ratio heuristic against
  `ReorderLevel`, not a consumption-rate/lead-time-demand model — that's explicitly M5 AI
  territory per the frontend mock's own prior comment.
- `Category.IsActive` and `StationeryItem.SupplierId` were added to the entities; the plan's
  §2.1 table didn't list either, but the service-layer behavior it specified (deactivate
  categories, block supplier deactivation when active items reference it) requires them.

### Not previously flagged: bootstrap admin account

Discovered during the seeder step, not anticipated in the plan: M1 deliberately seeds zero
users, but M2's seeded `StockTransaction` rows need a real `CreatedByEmployeeNumber`, and with
zero users there was also no way to sign in and create the first real Manager account at all
(`POST /api/v1/users` is Manager+-only — a closed loop with no way in). `DbSeeder.
SeedBootstrapAdminAsync` seeds exactly one Managing Director account; its password is read from
`Seed:BootstrapAdminPassword` config, never hardcoded. This is a genuine gap in the M1/M2
boundary, not just an M2 implementation detail — flag it if M1's design is ever revisited.

## Explicitly out of scope

- Live SQL Server migration and manual QA (see Tests above).
- `IStockService.IssueAsync` is implemented but unused — no M2 endpoint calls it; reserved for
  M4's request-fulfillment flow.
- The plan's two-branch git strategy (§9) — not followed; this was a single-implementer session
  and all 10 delivery steps landed as sequential commits on `main`, per the flag already added to
  the plan doc during its review.
- SKU (`[ASK]` #2) — not persisted, matching the plan; frontend SKU display/search now render
  blank rather than crash.

## Known issues

- `IdentityUserStore`'s `GetUsersAsync` does one role lookup per user per page (pre-existing from
  M1, unrelated to M2, noted here only because M2's `ItemQueries`/`InventoryQueries` follow the
  same "materialize then map" pattern for provider-safety, not for performance — fine at the
  plan's stated scale, worth revisiting if item/user counts grow far beyond ~1000).
- No image/SKU/barcode support, per `[CUT]`/`[ASK]` #2 — do not add without a Plan update.

## Seed-data correction — 2026-08-28

The live smoke test exposed two defects in seeded data (not in application code). Both are fixed
in `Infrastructure/Data/DbSeeder.cs`; **no schema or migration change was involved.**

1. **The catalogue was a cartesian product.** A single 8-item template was applied to all 5
   categories with the category name prepended — "Printing Supplies — Ballpoint Pens",
   "Organization — Premium Cardstock" — so only 8 distinct products existed across 40 rows.
   Replaced with `CatalogueSeeds`, a real per-category product list (still 5 × 8 = 40) and no
   name prefix. Now 40 distinct, correctly-categorised names.
2. **The low-stock path was unreachable.** Opening balance was `ReorderLevel * 3` against a
   net-positive random walk, so all 40 items finished `OK`: `lowStockAlerts` 0 and
   `/inventory/low-stock` empty, making the `WATCH`/`REORDER_NOW` badges and the dashboard
   low-stock tile impossible to demonstrate. Each item seed now carries a `StockPosture`
   (`Healthy`/`Watch`/`Reorder`); opening balance and issue probability vary by posture so low
   items drain across the 90 days, and one closing movement lands the balance in band via
   `TargetBalanceFor` (0.6× / 1.25× / 2.5× the item's own reorder level).

Result after re-seeding: **27 OK / 7 WATCH / 6 REORDER_NOW**, `lowStockAlerts` = 6, and the
ledger-vs-cached-balance invariant still holds across all 40 items. Suites re-run: 49/49 backend,
22/22 frontend.

The seeder is idempotent and skips a populated catalogue, so an **existing dev database will not
pick this up** — drop and re-create it (`dotnet ef database drop --force`, then start the app) to
get the corrected data.

Item names, costs, reorder levels and posture assignments are **invented plausible values**, not
`[SPEC]`-derived — the Plan specifies no product list. Replace them if the instructor supplies
real data.

## Reviewer follow-ups

1. ~~Apply the migration to a real SQL Server and do a manual browser smoke test.~~
   **Done 2026-08-28** — see the DB changes section above and the dated `AI_usage_report.md`
   entry. Still worth repeating on a second machine before the defence.
2. Confirm `[ASK]` #3 (role filter direction) and the lead-time unit with the instructor/team.
3. Decide whether the bootstrap-admin approach is acceptable long-term or should be replaced
   (e.g., a setup wizard, a seeded-from-config admin list) — this affects M1's design too.
4. Revisit inventory status thresholds once M5's real consumption-rate model exists.
5. Two reviewers required for stock/catalogue changes per `CLAUDE.md` §5.
6. **Decide how dev connects to SQL Server.** `appsettings.Development.json` targets
   `(localdb)\mssqllocaldb`, which is not installed on every machine; the 2026-08-28 run used a
   `ConnectionStrings__DefaultConnection` env var pointing at `.\SQLEXPRESS` instead. Either
   install LocalDB team-wide or change the checked-in dev default — a team decision, deliberately
   not made unilaterally.
