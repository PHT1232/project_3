# Supplier Request Cart (Inventory → Suppliers) — Implementation Handoff

> Implemented 2026-08-28 against `khang`. Full file-by-file detail is in the dated
> `AI_usage_report.md` entry; this is the architecture/flow summary for a reviewer picking it up
> cold.

## ⚠️ Scope status — read this first

**This feature builds something the Plan explicitly cut.** Plan §1.3's `[CUT] WON'T` row lists
*"payment or procurement PO generation"*, with the note *"If someone starts building one, that is
a scope breach — escalate to the Project Leader."* `CLAUDE.md` §5 repeats it.

The conflict was raised before any code was written, with the evidence above and an in-scope
alternative (a multi-item *goods receipt* cart, which needs no new entities). **The owning
developer chose to build the supplier request anyway, accepting the scope-breach risk.** That
decision is recorded here rather than left implicit.

Consequences a reviewer should weigh:

- The Plan's ~45-endpoint catalogue (§4.2) has no supplier-request endpoint, so `POST
  /api/v1/supplier-requests` is **not** `[SPEC]`-derived.
- The Plan is not updated. If this stays, §1.3 and §4.2 need revising the way K7 was closed,
  and the deviation belongs in the CRS.
- Nothing existing was removed to make room for it — the feature is purely additive.

## What it does

Inventory staff tick items in the inventory table, open **Request from Suppliers**, adjust each
line's quantity in a review modal, and submit. The server groups the lines by each item's
authoritative supplier and creates one order per supplier.

**Ordering is not receiving.** Creating a supplier request never touches `QuantityAvailable` and
never writes a `StockTransaction`. Stock still moves only through `IStockService`, when the goods
actually arrive via the row-level *Receive goods* action. This is asserted by an integration test
and was verified against live SQL Server.

## Architecture as built

Follows the existing M1/M2 layering exactly; no new patterns introduced.

- **`Core/Entities/SupplierRequest`** (header) + **`SupplierRequestItem`** (lines) — the same
  header/line split the Plan uses for `Requests`/`RequestItems` (§3.4), and `UnitCostSnapshot` is
  frozen per line so a later catalogue price edit never rewrites order history (CLAUDE.md
  principle #8).
- **`SupplierRequest` is NOT the Plan's `Requests` table.** That models an employee asking their
  superior for stationery (M3/M4, unbuilt). This models the inventory team ordering from a
  supplier. Different domains — do not merge them.
- **No status/lifecycle column.** No project document specifies what states a supplier order moves
  through, and inventing them is exactly what K3 flagged. When the team specifies a lifecycle it
  is an additive migration.
- **`SupplierRequestService` lives in `Infrastructure/Services/`**, next to `StockService`, for the
  same reason: it needs `DataContext` to write several entities in one unit of work, and
  Application must never reference `DbContext` (CLAUDE.md principle #1).
- **One `SaveChangesAsync` for the whole submission** — `DbContext` is the unit of work, no
  `UnitOfWork` wrapper (Plan §2.4). All validation runs *before* the first `Add`, so a cart with
  one bad line leaves nothing behind.

### The two rules the service exists to enforce

1. **The database owns the supplier, not the client.** If the item has a preferred
   `StationeryItem.SupplierId`, that wins outright and any client-supplied `supplierId` is
   ignored. The client value is consulted *only* for items that have no preferred supplier, and
   even then the supplier must exist and be active. Covered by
   `Submit_ClientSuppliedSupplierIsIgnoredWhenItemHasOne`.
2. **All-or-nothing.** Every line is validated up front; one invalid line rejects the whole
   submission with 400 and creates no partial orders. Covered by
   `Submit_OneInvalidItem_RollsBackEntireSubmission`.

## API contract

| Method | Route | Auth |
|---|---|---|
| POST | `/api/v1/supplier-requests` | Manager+ (`RequireManager`) |
| GET | `/api/v1/supplier-requests?page=&pageSize=` | Manager+ |
| GET | `/api/v1/supplier-requests/{id}` | Manager+ |

**Request**

```json
{ "items": [ { "itemId": 25, "quantity": 10, "supplierId": null } ] }
```

`supplierId` is only read for items with no preferred supplier; send `null` otherwise.

**201 Created** — one entry per distinct supplier, already grouped:

```json
[
  { "supplierRequestId": 1, "supplierId": 1, "supplierName": "OfficeMax Direct",
    "totalCost": 58.00, "createdAtUtc": "...", "createdByEmployeeNumber": 1,
    "items": [ { "itemId": 25, "itemName": "Lever Arch File, A4",
                 "quantity": 10, "unitCostSnapshot": 4.60, "lineTotal": 46.00 } ] }
]
```

**Errors** — standard RFC 7807 via the existing `ExceptionHandlingMiddleware`:
`400` validation (empty cart, quantity ≤ 0, unknown/inactive item, unresolved or inactive
supplier, duplicate item lines — all reported under `errors.items[]`), `401` unauthenticated,
`403` not Manager+.

## DB changes

One migration, `20260828143526_SupplierRequests`: adds `SupplierRequests` and
`SupplierRequestItems`. **No existing table is altered**, so there is no data-loss risk.

- `CK_SupplierRequestItems_Quantity` (`[Quantity] > 0`)
- unique index on `(SupplierRequestId, ItemId)` — an item may appear once per order, enforced in
  the database as well as the validator
- all FKs `Restrict` except the header→lines cascade; `CreatedByEmployeeNumber` → `AspNetUsers`,
  scalar-only, matching `StockTransaction` (K8)

**Applied to real SQL Server** (`.\SQLEXPRESS`) and exercised end to end on 2026-08-28.

## Frontend

- `InventoryPage.jsx` — the pre-existing but previously unused `selectedIds` state became the
  cart, plus `cartQuantities`. Both live above the filter/sort logic, so searching, filtering or
  re-sorting never drops the cart. Plain `useState` — no Redux, per Plan §2.4.
- `components/SupplierRequestModal.jsx` — review table (item · supplier · in stock · quantity ·
  remove), per-line quantity editing, estimated total, loading/error/success states, and grouped
  success feedback.
- `api/supplierRequests.js` — the only place these endpoints are called from.
- `InventoryRowDto` gained `supplierId`/`supplierName` (additive, defaulted, so nothing that
  consumed it before breaks).

### Deviation from the requested UI, flagged

The brief put the cart behind the existing **Receive Goods** button. It is instead a **new
"Request from Suppliers" button**, because "Receive Goods" now has to mean two opposite things —
the row-level action really does receive stock, while the cart only raises an order. Two
identically-labelled controls with opposite effects is a demo hazard. Existing *Adjust Stock* and
*Receive Goods* behaviour is untouched. Rename it if the team prefers the original label.

## Tests actually run

- `dotnet test Project.slnx` — **63/63 passed** (26 unit + 37 integration; was 49 before,
  +14 new in `SupplierRequestsTests`).
- `npx vitest run --pool=threads` — **29/29 passed** (7 files; was 22, +7 new in
  `InventoryCart.test.jsx`).
- `dotnet build` and `npm run build` — both clean.
- **Live end-to-end** against SQL Server: 3 items spanning 3 suppliers → 3 orders created with
  correct grouping and totals; SQL confirmed **stock unchanged, zero ledger rows written**, and
  the ledger-vs-cached-balance invariant still holding across all 40 items. Selection and cart
  cleared on success; Catalogue (40 items, filters) and Inventory both still work.

Coverage includes: single item, multiple items, multiple suppliers, independent quantities, empty
cart, quantity 0 and negative, unknown item, inactive item, missing supplier, client-supplied
supplier override attempt, duplicate lines, Engineer → 403, unauthenticated → 401, rollback, and
stock-not-increased.

## Assumptions and known gaps

- **Default cart quantity is 1.** A "suggested reorder amount" would be more useful but is an
  invented business rule; left for the team to specify.
- **SKU is still not persisted** (`[ASK]` #2 / K5), so the cart shows no SKU column even though
  the brief asked for one. Adding it is a separate scope decision — `page-map.md:194` currently
  says do not build it.
- **No order lifecycle.** Orders cannot be marked received, cancelled, or reconciled against a
  later goods receipt. There is no link between a `SupplierRequest` and the `StockTransaction`
  that eventually fulfils it — deliberately, pending a spec.
- **No UI to browse past orders.** `GET /supplier-requests` exists and is tested, but nothing in
  the frontend lists them yet.
- The toolbar **Adjust Stock** button still acts on `visibleRows[0]` rather than the selection —
  pre-existing behaviour, deliberately left alone.

## Reviewer follow-ups

1. **Decide whether this feature stays.** If yes, update Plan §1.3/§4.2 and record the deviation
   in the CRS. If no, the change is cleanly revertible — it is additive.
2. Two reviewers required for stock/catalogue changes (`CLAUDE.md` §5).
3. Confirm the default cart quantity and whether a suggested reorder amount is wanted.
4. Decide whether the order lifecycle (and the order↔receipt link) is in scope for M4.
5. Confirm the "Request from Suppliers" button label.
