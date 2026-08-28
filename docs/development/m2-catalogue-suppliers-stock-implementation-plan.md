# M2 — Catalogue, Suppliers & Stock Ledger Implementation Plan

> Planned 2026-08-28 against `main` at `93e3b25`.
>
> Scope: Catalogue (items, categories), Suppliers, Stock ledger (StockTransaction), Inventory endpoints.
> Owner: M2 (catalogue/suppliers), M3 (inventory/ledger).
> Duration: Day 3–6 (overlaps M1).
> Dependencies: M0 (schema, shell). M1 (auth/user-management) shipped 2026-08-27 — the real
> `ICurrentUserService`, `RequireManager` policy, JWT auth, and Identity-backed `DataContext` all
> exist now. **Do not build a stub `ICurrentUserService`** — consume the real one directly.

## Required reading for implementing agents

Before writing catalogue/supplier/inventory code, read these in order:

1. `CLAUDE.md`, especially §6 K8 (Identity replaces the `Users` table — see §2.3 below).
2. `__ai_agents/Stationery_Management_System_Project_Plan.md` §2–§4.
3. `docs/development/identity-and-user-management-implementation-plan.md` — the auth/user
   surface this plan builds on.
4. This document.
5. Existing implementation to reuse, not duplicate:
   - `Application/Interfaces/Auth/ICurrentUserService.cs` and `WebApi/Services/CurrentUserService.cs`
   - `WebApi/Authorization/RankLevelRequirement.cs` (the `RequireManager` policy)
   - `Infrastructure/DataContext.cs`, `Infrastructure/Identity/ApplicationUser.cs`
   - `Infrastructure/Data/DbSeeder.cs` and its call site in `WebApi/Program.cs`
   - `Tests/WebApi.IntegrationTests/CustomWebApplicationFactory.cs` and `TestUserFactory.cs`
6. `AI_usage_report.md`; append new records and never overwrite existing entries.

---

## 1. Architecture decision

Follow the established Clean Architecture boundary:

- **Core**: Domain entities, repository interfaces (`IRepository<T>`), domain enums.
- **Application**: Use-case services, DTOs, validators, query interfaces (`IItemQueries`, `ISupplierQueries`, `IStockService`, `IInventoryQueries`).
- **Infrastructure**: EF Core entities, configurations, migrations, DbContext, repository implementations, query implementations, seeder extensions.
- **WebApi**: Controllers, authorization policies, middleware, Program.cs registration.

Frontend contracts already documented in `frontend/src/api/catalogue.js` and `frontend/src/api/inventory.js`. Replace mock implementations with real API calls when backend lands.

Do not add MediatR, AutoMapper, UnitOfWork, SignalR, or soft-delete on catalogue tables. Stock is a ledger: `StockTransactions` append-only, `QuantityAvailable` cached balance.

---

## 2. Database schema (EF Core migrations only)

### 2.1 Core domain entities (Core/Entities)

| Entity | Key fields | Notes |
|---|---|---|
| `Category` | `Id`, `Name` | Lookup table, no RowVersion |
| `Supplier` | `Id`, `Name`, `LeadTimeDays`, `IsActive` | RowVersion for concurrency |
| `StationeryItem` | `Id`, `ItemName`, `CategoryId`, `UnitOfMeasure`, `UnitCost`, `QuantityAvailable`, `ReorderLevel`, `MinRankLevelToRequest`, `IsActive`, `RowVersion` | Cost is `decimal(18,2)`. `MinRankLevelToRequest` maps to Role.RankLevel (Engineer=1, Manager=2, Business Manager=3, MD=4). |

### 2.2 Stock ledger (Core/Entities)

| Entity | Key fields | Notes |
|---|---|---|
| `StockTransaction` | `Id`, `ItemId`, `TxType` (enum: `Receipt`, `Issue`, `Adjustment`), `ChangeQuantity`, `UnitCostSnapshot`, `Reference`, `SupplierId?`, `CreatedAtUtc`, `CreatedByEmployeeNumber` | Append-only. No Update/Delete. Balance = SUM(ChangeQuantity). |

### 2.3 EF Core configurations (Infrastructure/Data/Configurations)

- `CategoryConfiguration`: `Name` required, max 100.
- `SupplierConfiguration`: `Name` required, max 200. `LeadTimeDays` required, ≥0. `RowVersion` concurrency token.
- `StationeryItemConfiguration`: `ItemName` required, max 200. `UnitOfMeasure` required, max 50. `UnitCost` precision 18,2. `QuantityAvailable` default 0. `ReorderLevel` default 0. `MinRankLevelToRequest` default 1. `RowVersion` concurrency token. FK to `Category` (restrict delete). Check constraint `IsActive` + `MinRankLevelToRequest BETWEEN 1 AND 4`.
- `StockTransactionConfiguration`: `TxType` required (int enum). `ChangeQuantity` non-zero. `UnitCostSnapshot` precision 18,2. `CreatedAtUtc` default `GETUTCDATE()`. FK to `StationeryItem` (cascade delete? No, restrict — ledger rows must survive item deactivation). FK to `Supplier` nullable. FK on `CreatedByEmployeeNumber` restrict.

  **`Users` does not exist as a table.** `__ai_agents/Database/StationerySchema.sql`'s `Users` table (and its `REFERENCES [Users]([EmployeeNumber])` FKs) is the pre-M1 schema, superseded by ASP.NET Core Identity (K8, resolved 2026-08-27 — see `CLAUDE.md` §6). The actual table is `AspNetUsers`, and the actual EF entity to FK against is `Infrastructure.Identity.ApplicationUser` (`Id` is the employee number). Configure `CreatedByEmployeeNumber` as a FK to `ApplicationUser`, not `Users` — this needs a `ProjectReference`/`using` on `Infrastructure.Identity` from wherever `StockTransactionConfiguration` lives, same as `ApplicationUserConfiguration` already does.

### 2.4 Migration

Create **one reviewed migration** for all M2 tables. Announce before PR (only one migration PR active at a time).

---

## 3. Application layer (Application/)

### 3.1 DTOs (Application/DTOs/)

**Catalogue:**

- `CategoryDto.cs`: `CategoryId`, `Name`
- `ItemDto.cs`: `ItemId`, `ItemName`, `CategoryId`, `CategoryName`, `UnitOfMeasure`, `UnitCost`, `QuantityAvailable`, `ReorderLevel`, `MinRankLevelToRequest`
- `CreateItemRequest.cs`, `UpdateItemRequest.cs`
- `ItemQueryParameters.cs`: `Page`, `PageSize`, `CategoryId?`, `SearchTerm?`, `IncludeInactive?` (Manager+ only)

**Suppliers:**

- `SupplierDto.cs`: `SupplierId`, `Name`, `LeadTimeDays`, `IsActive`
- `CreateSupplierRequest.cs`, `UpdateSupplierRequest.cs`

**Inventory (Manager+):**

- `InventoryRowDto.cs`: `ItemId`, `ItemName`, `Sku?`, `QuantityAvailable`, `ReorderLevel`, `UnitCost`, `Status` (enum: `OK`, `WATCH`, `REORDER_NOW`)
- `InventorySummaryDto.cs`: `TotalItems`, `LowStockAlerts`, `TotalValue`
- `AdjustStockRequest.cs`: `ChangeQuantity` (int ≠ 0), `Reason` (required, max 500)
- `ReceiveGoodsRequest.cs`: `Quantity` (>0), `SupplierId?`, `Reference?`
- `StockTransactionDto.cs`: `TransactionId`, `ItemId`, `TxType`, `ChangeQuantity`, `UnitCostSnapshot`, `Reference`, `SupplierId?`, `CreatedAtUtc`, `CreatedByEmployeeNumber`, `CreatedByName` — `CreatedByName` comes from joining `ApplicationUser.Name` (via `AspNetUsers`), not a `Users` table (see §2.3).

### 3.2 Interfaces (Application/Interfaces/)

`callerRankLevel` below is non-nullable `int`, but `ICurrentUserService.RankLevel` is `int?`
(claims-derived). Controllers must resolve it before calling in, e.g.
`currentUserService.RankLevel ?? 0` — `[Authorize]` guarantees an authenticated caller has a
rank claim in practice, but the interface itself doesn't, so don't assume non-null without the guard.

```csharp
// Catalogue
public interface IItemService
{
    Task<PagedResult<ItemDto>> GetItemsAsync(ItemQueryParameters parameters, int callerRankLevel);
    Task<ItemDto?> GetItemByIdAsync(int itemId, int callerRankLevel);
    Task<ItemDto> CreateItemAsync(CreateItemRequest request);
    Task<ItemDto> UpdateItemAsync(int itemId, UpdateItemRequest request);
    Task DeactivateItemAsync(int itemId); // never delete
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto> CreateCategoryAsync(string name);
    Task<CategoryDto> UpdateCategoryAsync(int categoryId, string name);
    Task DeactivateCategoryAsync(int categoryId);
}

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetSuppliersAsync(int page, int pageSize, bool includeInactive);
    Task<SupplierDto?> GetSupplierByIdAsync(int supplierId);
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request);
    Task<SupplierDto> UpdateSupplierAsync(int supplierId, UpdateSupplierRequest request);
    Task DeactivateSupplierAsync(int supplierId); // 409 if active items reference it
}

// Inventory (Manager+)
public interface IInventoryService
{
    Task<InventoryPageResult> GetInventoryAsync(int page, int pageSize);
    Task<IReadOnlyList<InventoryRowDto>> GetLowStockAsync();
    Task<InventoryRowDto> AdjustStockAsync(int itemId, AdjustStockRequest request, int actorEmployeeNumber);
    Task<InventoryRowDto> ReceiveGoodsAsync(int itemId, ReceiveGoodsRequest request, int actorEmployeeNumber);
    Task<IReadOnlyList<StockTransactionDto>> GetTransactionHistoryAsync(int itemId);
}
```

### 3.3 Validators (Application/Validators/)

- `CreateItemRequestValidator`: ItemName required 1–200, UnitOfMeasure required 1–50, UnitCost ≥ 0, ReorderLevel ≥ 0, CategoryId exists, MinRankLevelToRequest 1–4.
- `UpdateItemRequestValidator`: Same as create but all optional except at least one field.
- `CreateSupplierRequestValidator`: Name required 1–200, LeadTimeDays ≥ 0.
- `UpdateSupplierRequestValidator`: Same as create but optional.
- `AdjustStockRequestValidator`: ChangeQuantity ≠ 0, Reason required 1–500.
- `ReceiveGoodsRequestValidator`: Quantity > 0.

### 3.4 Services (Application/Services/)

- `ItemService`: Implements `IItemService`. Role filtering: `WHERE IsActive = 1 AND MinRankLevelToRequest <= @callerRankLevel`. Uses `IRepository<StationeryItem>` + `IItemQueries` for paged list.
- `CategoryService`: Implements `ICategoryService`. Simple CRUD via `IRepository<Category>`.
- `SupplierService`: Implements `ISupplierService`. Deactivate checks `StationeryItems.Any(i => i.SupplierId == id && i.IsActive)` → 409.
- `InventoryService`: Implements `IInventoryService`. Uses `IInventoryQueries` for reads, `IStockService` for writes. All methods require `RequireManager` policy enforced at controller.

---

## 4. Infrastructure layer (Infrastructure/)

### 4.1 EF Core entities (Infrastructure/Entities/ or Infrastructure/Data/)

Map 1:1 to Core entities. Add EF Core annotations.

**Concurrency token: `Guid RowVersion`, app-managed — not SQL Server's native `rowversion`/`timestamp`.**
`StationeryItem` and `Supplier` need a concurrency token, but integration tests (§7.2) run against
EF Core **SQLite in-memory** (`Tests/WebApi.IntegrationTests/CustomWebApplicationFactory.cs`), and
SQLite has no equivalent to SQL Server's DB-auto-updated `rowversion` column — `byte[]` +
`.IsRowVersion()` behaves differently (or not at all) across the two providers. Use a `Guid`
property configured with `.IsConcurrencyToken()` (not `.IsRowVersion()`) and reassign it to
`Guid.NewGuid()` explicitly in application code on every mutating save (in `StockService`, and in
`ItemService`/`SupplierService`'s update paths) so the behavior is identical on SQL Server and
SQLite. This is a deliberate deviation from the original `byte[] RowVersion` design — resolve it
this way before Step 1, since it's part of the entity shape.

### 4.2 DbContext (Infrastructure/DataContext.cs)

`DataContext` is `IdentityDbContext<ApplicationUser, ApplicationRole, int>` as of M1, not a plain
`DbContext` — this doesn't change what M2 needs to do, just don't expect a blank slate. Add
`DbSet<Category>`, `DbSet<Supplier>`, `DbSet<StationeryItem>`, `DbSet<StockTransaction>`.
Configuration classes are picked up automatically via the existing
`builder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly)` call in `OnModelCreating` —
no change needed there, just add the new `IEntityTypeConfiguration<T>` classes.

### 4.3 Repository implementations

- `CategoryRepository : IRepository<Category>`
- `SupplierRepository : IRepository<Supplier>`
- `ItemRepository : IRepository<StationeryItem>`
- `StockTransactionRepository : IRepository<StockTransaction>`

Simple CRUD only. Complex queries go to dedicated query implementations.

### 4.4 Query implementations (Infrastructure/Queries/)

- `ItemQueries : IItemQueries` — paged, filtered, role-aware SQL via EF Core LINQ.
- `SupplierQueries : ISupplierQueries`
- `InventoryQueries : IInventoryQueries` — joins Item + latest balance + status derivation.
- `StockQueries : IStockQueries` — transaction history; `CreatedByName` joins `ApplicationUser` (see §2.3 — there is no `Users` table).

### 4.5 Stock service (Infrastructure/Services/StockService.cs)

```csharp
public class StockService : IStockService
{
    private readonly DataContext _db;
    public async Task IssueAsync(int itemId, int quantity, int actorEmployeeNumber, string reference) { ... }
    public async Task ReceiveAsync(int itemId, int quantity, int? supplierId, string reference, int actorEmployeeNumber) { ... }
    public async Task AdjustAsync(int itemId, int changeQuantity, string reason, int actorEmployeeNumber) { ... }
}
```

Each method:
1. Loads `StationeryItem` with its `RowVersion` (`Guid`, app-managed — see §4.1).
2. Updates `QuantityAvailable += changeQuantity` (Issue: negative, Receive: positive, Adjust: signed).
3. Reassigns `RowVersion = Guid.NewGuid()` on the loaded entity before saving.
4. Creates `StockTransaction` row in same `SaveChangesAsync()`.
5. Concurrency: stale `RowVersion` on the incoming request vs. the freshly-loaded entity →
   `DbUpdateConcurrencyException` → map to 409.

### 4.6 Seeder extension (Infrastructure/Data/DbSeeder.cs)

Add `SeedCatalogueAndInventoryAsync()`:
- 5 Categories
- 6 Suppliers (with LeadTimeDays)
- 40 StationeryItems (spread across categories, various MinRankLevelToRequest, UnitCost, ReorderLevel)
- 90 days of StockTransactions (Receipt/Issue/Adjustment) so M5 AI has consumption history.

Call it from the same startup block that already runs `SeedRolesAsync` — `WebApi/Program.cs`'s
`if (!app.Environment.IsEnvironment("Testing"))` block, right after `DbSeeder.SeedRolesAsync(roleManager)`
— not a new block. Idempotent. Integration tests need their own explicit seed call inside test
setup (mirroring `Tests/WebApi.IntegrationTests/TestUserFactory.cs`'s pattern), since that
`Testing`-guarded block is skipped for `CustomWebApplicationFactory`.

---

## 5. Web API layer (WebApi/)

### 5.1 Controllers

**CatalogueController** (`[Authorize]`, any role):
- `GET /api/v1/categories` → `ICategoryService.GetCategoriesAsync()`
- `GET /api/v1/items` → `IItemService.GetItemsAsync()` (pass caller's RankLevel from `ICurrentUserService`)
- `GET /api/v1/items/{id}` → `IItemService.GetItemByIdAsync()`

**ManagerCatalogueController** (`[Authorize(Policy = "RequireManager"]`):
- `POST /api/v1/items` → `IItemService.CreateItemAsync()`
- `PUT /api/v1/items/{id}` → `IItemService.UpdateItemAsync()`
- `PATCH /api/v1/items/{id}/deactivate` → `IItemService.DeactivateItemAsync()`
- `POST /api/v1/categories` → `ICategoryService.CreateCategoryAsync()`
- `PUT /api/v1/categories/{id}` → `ICategoryService.UpdateCategoryAsync()`
- `PATCH /api/v1/categories/{id}/deactivate` → `ICategoryService.DeactivateCategoryAsync()`

**SuppliersController** (`[Authorize(Policy = "RequireManager"]`):
- `GET /api/v1/suppliers` → `ISupplierService.GetSuppliersAsync()`
- `GET /api/v1/suppliers/{id}` → `ISupplierService.GetSupplierByIdAsync()`
- `POST /api/v1/suppliers` → `ISupplierService.CreateSupplierAsync()`
- `PUT /api/v1/suppliers/{id}` → `ISupplierService.UpdateSupplierAsync()`
- `PATCH /api/v1/suppliers/{id}/deactivate` → `ISupplierService.DeactivateSupplierAsync()`

**InventoryController** (`[Authorize(Policy = "RequireManager"]`):
- `GET /api/v1/inventory` → `IInventoryService.GetInventoryAsync()`
- `GET /api/v1/inventory/low-stock` → `IInventoryService.GetLowStockAsync()`
- `POST /api/v1/inventory/{itemId}/adjust` → `IInventoryService.AdjustStockAsync()`
- `POST /api/v1/inventory/{itemId}/receive` → `IInventoryService.ReceiveGoodsAsync()`
- `GET /api/v1/inventory/{itemId}/transactions` → `IInventoryService.GetTransactionHistoryAsync()`

### 5.2 Authorization

- Catalogue read: any authenticated user (JWT valid).
- Catalogue write, Suppliers, Inventory: `RequireManager` policy (RankLevel ≥ 2).
- `ICurrentUserService` provides `EmployeeNumber`, `RankLevel`, `Role`.

### 5.3 Registration (WebApi/Program.cs)

Register all Application services, Infrastructure query implementations, `IStockService`, `DbContext`, FluentValidation.

---

## 6. Frontend integration

### 6.1 Catalogue page (`frontend/src/pages/Catalogue.jsx`)

- Grid/list view using existing `Card`, `Table`, `SearchInput`, `Badge` components.
- Category filter dropdown (from `getCategories()`).
- Search input (debounced).
- Stock badge: uses `frontend/src/lib/availability.js` logic (OK / LOW / OUT).
- "Add to request" button (stub: emits event / adds to cart context for M3).
- Loading, error, empty states.

### 6.2 Manager Item Management (`frontend/src/pages/manager/ItemManagement.jsx`)

- Table with create/edit/deactivate modal forms.
- Client + server validation (FluentValidation rules mirrored in Zod/Yup or HTML5).
- Role restriction field (dropdown: Engineer/Manager/Business Manager/MD).

### 6.3 Manager Supplier Management (`frontend/src/pages/manager/SupplierManagement.jsx`)

- Similar CRUD table with lead-time field.

### 6.4 Inventory page (`frontend/src/pages/Inventory.jsx`)

- Table with `InventoryRowDto` columns.
- Status badge (OK / WATCH / REORDER_NOW) from server-derived `Status`.
- Summary cards (Total Items, Low Stock Alerts, Total Value).
- "Adjust Stock" and "Receive Goods" actions (modals with reason/reference fields).
- Low-stock filter/button.

### 6.5 API client updates

Replace mock implementations in:
- `frontend/src/api/catalogue.js` → real `client.get('/categories')`, `client.get('/items')`
- `frontend/src/api/inventory.js` → real `client.get('/inventory')`, `client.post('/inventory/{id}/adjust')`, etc.
Delete mock files after verification.

---

## 7. Tests

### 7.1 Unit tests (Tests/Application.UnitTests/)

- `ItemServiceTests`: Role filtering, CRUD, deactivate.
- `SupplierServiceTests`: Deactivate 409 when active items exist.
- `InventoryServiceTests`: Adjust/Receive happy path, 400 validation, 409 concurrency.
- `StockServiceTests`: Balance = SUM(ChangeQuantity) invariant.

### 7.2 Integration tests (Tests/WebApi.IntegrationTests/)

- `CatalogueControllerTests`: GET items returns role-filtered list.
- `InventoryControllerTests`: Adjust/Receive persists StockTransaction, updates balance, concurrency 409.
- `SuppliersControllerTests`: Deactivate 409 on active reference.

### 7.3 Frontend tests (Vitest + RTL)

- `Catalogue.test.jsx`: renders grid, filters, search, empty state.
- `Inventory.test.jsx`: renders table, status badges, adjust/receive modals.

---

## 8. Delivery steps (commits)

| Step | Scope | Files (representative) |
|---|---|---|
| 1 | Core entities + repository interfaces | `Core/Entities/{Category,Supplier,StationeryItem,StockTransaction}.cs`, `Core/Interfaces/{IItemRepository,ISupplierRepository,IStockTransactionRepository}.cs` |
| 2 | Application DTOs, interfaces, validators | `Application/DTOs/{Catalogue,Suppliers,Inventory}/*.cs`, `Application/Interfaces/{Catalogue,Suppliers,Inventory}/*.cs`, `Application/Validators/{Catalogue,Suppliers,Inventory}/*.cs` |
| 3 | Application services | `Application/Services/Catalogue/{Item,Category,Supplier}Service.cs`, `Application/Services/Inventory/InventoryService.cs` |
| 4 | Infrastructure EF entities, configs, DbContext, migration | `Infrastructure/Entities/*.cs`, `Infrastructure/Data/Configurations/*.cs`, `Infrastructure/DataContext.cs`, `Infrastructure/Data/Migrations/20260828_*_CatalogueAndStock.cs` |
| 5 | Infrastructure repositories, queries, StockService | `Infrastructure/Repositories/*.cs`, `Infrastructure/Queries/*.cs`, `Infrastructure/Services/StockService.cs` |
| 6 | Seeder extension | `Infrastructure/Data/DbSeeder.cs` (SeedCatalogueAndInventoryAsync) |
| 7 | WebApi controllers + registration | `WebApi/Controllers/{Catalogue,Suppliers,Inventory}Controller.cs`, `WebApi/Program.cs` |
| 8 | Frontend Catalogue page + Manager Item/Supplier pages | `frontend/src/pages/Catalogue.jsx`, `frontend/src/pages/manager/ItemManagement.jsx`, `frontend/src/pages/manager/SupplierManagement.jsx` |
| 9 | Frontend Inventory page + API client swap | `frontend/src/pages/Inventory.jsx`, `frontend/src/api/catalogue.js`, `frontend/src/api/inventory.js` (remove mocks) |
| 10 | Tests (unit + integration + frontend) | `Tests/Application.UnitTests/*`, `Tests/WebApi.IntegrationTests/*`, `frontend/src/**/*.test.jsx` |

Each step = one commit, PR < 400 lines, squash merge, 2 reviewers for stock/catalogue.

---

## 9. Git strategy

**Flagged, not resolved here:** this assumes M2 and M3 are two different people (per CLAUDE.md
§6 K6, "who is M1/M2/M3" is itself unresolved). All M1 work in this repo was done by a single
agent across every "owner" the identity plan named. If M2/M3 are likewise one implementer, the
two-branch rebase choreography below is unnecessary ceremony — collapse it to one branch and
treat §8's steps as sequential commits instead. Confirm which applies before Step 1.

Two branches off `main` (if M2 and M3 are in fact different people):
- `feat/M2-catalogue` (M2 owner): Steps 1–5, 7–8 (catalogue/suppliers)
- `feat/M2-inventory` (M3 owner): Steps 1, 4 (shared entities/migration), 5 (StockService), 6, 7 (inventory), 9 (inventory UI)

**M3 rebases onto M2's branch before opening PR** to resolve shared migration once. Merge catalogue first, then inventory. Tag `v0.3.0-catalogue` after both.

---

## 10. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Migration conflict M2/M3 | High | Medium | Rebase rule above; M3 is sole migration custodian |
| Role filter interpreted wrongly | Medium | Medium | Isolated in one query filter; `[ASK]` #3 at Week-1 review |
| Stock balance drifts from ledger | Medium | High | Reconciliation test in CI; single write path through `IStockService` |
| Scope creep into images/uploads | Medium | Medium | `[CUT]`. Use `lucide-react` category icons |
| Concurrency bugs under load | Low | High | RowVersion on all mutable entities; integration test for 409 |

---

## 11. Definition of Done

- All backend endpoints return 200/400/401/403/404/409 per spec; ProblemDetails format.
- Frontend pages render loading/error/empty states; no console errors.
- `dotnet build Project.slnx` clean (warnings ≤ pre-existing).
- `dotnet test Project.slnx` passes.
- `npm run build` passes.
- `npx vitest run` passes.
- Migration applies cleanly to clean SQL Server (tested via `docker compose up`).
- Seeder runs idempotently; 40 items, 6 suppliers, 5 categories, 90 days transactions present.
- `GET /items` respects caller RankLevel (Engineer sees ≤1, Manager sees ≤2, etc.).
- Stock adjustment/receive creates ledger row + updates balance atomically.
- Concurrency: simultaneous adjust on same item → second request gets 409.

---

## 12. Open questions (flag for instructor/team)

1. **[ASK] #3**: Role filter — `MinRankLevelToRequest <= caller.RankLevel` (default) or `MinRankLevelToRequest == caller.RankLevel`? Plan says "role-filtered catalogue" but exact rule is ambiguous.
2. **SKU field**: Frontend mock includes `sku` on `InventoryRowDto`. Plan §1.3 lists "barcode/SKU" as future improvement → do not persist, compute or omit for now.
3. **Supplier lead time unit**: Days (integer) assumed. Confirm.
4. **Initial stock seed**: Should seeder create opening balance via `StockTransaction` type `Receipt` with `Reference = "OPENING"`? Yes, for ledger integrity.

---

## 13. Documentation updates required after implementation

- Update `docs/development/identity-and-user-management-implementation-plan.md` if any auth boundary changes.
- Create `docs/development/catalogue-inventory-implementation-handoff.md` with architecture, flow, files, APIs, DB changes, setup, tests run, assumptions, exclusions, known issues, reviewer follow-ups.
- Append to `AI_usage_report.md`.

---

## 14. Estimated hours (per Plan §6.1)

| Track | Owner | Est. |
|---|---|---|
| T2.1 Entities + migration | M2 | 5h |
| T2.2 Item CRUD + role filter | M2 | 7h |
| T2.3 Supplier CRUD | M2 | 5h |
| T2.4 Catalogue UI | M2 | 7h |
| T2.5 Manager Item/Supplier UI | M2 | 6h |
| T2.6 StockTransaction + IStockService | M3 | 6h |
| T2.7 Inventory endpoints + UI | M3 | 5h |
| T2.8 Seeder extension | M3 | 3h |
| **Total** | | **44h** |

---

## 15. Next action

Begin **Step 1**: Core entities + repository interfaces. Create `Core/Entities/` folder if not exists, add four entities with proper annotations, add repository interfaces to `Core/Interfaces/`.