# Handoff — Goods-arrival confirmation (stock no longer rises before delivery)

**Date:** 2026-09-04 · **Branch:** `khang` · **Migration:** `20260904104551_AddSupplierOrderArrivalStatus` (announce it — one open migration PR at a time)

## 1. The problem

Inventory went up the moment someone *recorded* incoming stock, so the system could show goods as
available that had never physically arrived.

Concretely: `POST /api/v1/inventory/{itemId}/receive` (the "Receive Goods" modal on the Inventory
page) called `StockService.ReceiveAsync`, which incremented `QuantityAvailable` immediately. Any
Manager could do it, against any item, with no order and no delivery behind it.

The purchase record itself — `SupplierRequest` — was already correct: creating one moved no stock.
But it had no lifecycle at all, so there was nowhere to record "these goods turned up".

## 2. The flow now

```
Manager raises a supplier order          POST /supplier-requests
        ↓                                 Status = PendingArrival   ← stock UNCHANGED
Goods physically arrive
        ↓
Business Manager confirms                POST /supplier-requests/{id}/confirm-arrival
        ↓                                 rank >= 3 only; 403 otherwise
Status = Received                         one Receipt ledger row per line
        ↓                                 balances + status in ONE transaction
Inventory quantity updated — once         confirming again → 409, nothing posted
```

`POST /inventory/{itemId}/adjust` is unchanged: it is for corrections (damage, stocktake), not
for receiving.

## 3. Why the direct receive endpoint was removed

Leaving `{itemId}/receive` in place would have made the whole control optional — a Manager could
still raise the balance with no delivery, which is the bug the change exists to fix. Stock now has
exactly two entry points: a confirmed arrival, or an explicit manual adjustment. `IStockService`
remains the single write path onto the ledger (CLAUDE.md #5).

`IStockService.ReceiveAsync` was deleted with it and replaced by `StageReceiptAsync`, which stages
the balance change and ledger row **without saving** so the caller commits everything at once —
the same "stage, caller saves" contract `INotificationService.NotifyRequestEventAsync` already
uses in this codebase.

## 4. Authorisation

| Role | Rank | Raise an order | Confirm arrival |
|---|---|---|---|
| Engineer | 1 | ✗ 403 | ✗ 403 |
| Manager | 2 | ✓ | ✗ 403 |
| Business Manager | 3 | ✓ | ✓ |
| Managing Director | 4 | ✓ | ✓ |

The controller keeps its `RequireManager` default and the confirm action narrows to
`RequireBusinessManager` — a Manager may order, but only a Business Manager certifies that goods
arrived. MD passes because rank policies are `>=`, consistent with every other policy here.
Hiding the button in the UI is UX only; the server returns 403 regardless (Plan §2.5).

## 5. Duplicate protection

`ConfirmArrivalAsync` refuses any order not in `PendingArrival` with a `ConflictException` → 409,
and the status flip commits in the same transaction as the receipts. A second click posts nothing.

**Limitation:** two confirmations racing in *separate* transactions could both read
`PendingArrival` — `SupplierRequest` has no RowVersion concurrency token. Sequential double-clicks
(the realistic case) are fully covered. Add a token if the team wants the race closed.

## 6. Files

| Layer | File | Change |
|---|---|---|
| Core | `Entities/SupplierRequest.cs` | `Status`, `ReceivedAtUtc`, `ReceivedByEmployeeNumber` + status constants |
| Infrastructure | `Data/Configurations/SupplierRequestConfiguration.cs` | columns, `CK_SupplierRequests_Status`, status index, FK |
| Infrastructure | `Data/Migrations/20260904104551_AddSupplierOrderArrivalStatus.*` | **new** — see §7 |
| Infrastructure | `Services/SupplierRequestService.cs` | `ConfirmArrivalAsync` |
| Infrastructure | `Services/StockService.cs` | `StageReceiptAsync` added, `ReceiveAsync` removed |
| Infrastructure | `Queries/SupplierRequestQueries.cs` | new fields + confirmer name |
| Application | `Interfaces/SupplierRequests/ISupplierRequestService.cs` | `ConfirmArrivalAsync` |
| Application | `Interfaces/Inventory/IStockService.cs`, `IInventoryService.cs` | `StageReceiptAsync`; `ReceiveGoodsAsync` removed |
| Application | `Services/Inventory/InventoryService.cs` | `ReceiveGoodsAsync` removed |
| Application | `DTOs/SupplierRequests/SupplierRequestDto.cs` | + status/received fields |
| Application | `DTOs/Inventory/ReceiveGoodsRequest.cs`, `Validators/…/ReceiveGoodsRequestValidator.cs` | **deleted** |
| WebApi | `Controllers/SupplierRequestsController.cs` | `POST {id}/confirm-arrival` |
| WebApi | `Controllers/InventoryController.cs` | `{itemId}/receive` **deleted** |
| Frontend | `api/supplierRequests.js` | `confirmSupplierRequestArrival`, `SUPPLIER_ORDER_STATUS` |
| Frontend | `api/inventory.js` | `receiveGoods` removed |
| Frontend | `pages/inventory/components/SupplierOrdersModal.jsx` | **new** — order list + "Goods Arrived" |
| Frontend | `pages/inventory/InventoryPage.jsx` | "Receive Goods" → "Supplier Orders"; rank gate |
| Frontend | `pages/inventory/components/InventoryTable.jsx`, `StockActionModal.jsx` | receive action removed |
| Tests | `SupplierRequestsTests.cs` (+7), `InventoryTests.cs`, `InventoryCart.test.jsx` | see §8 |

## 7. The migration

Adds the three columns, the check constraint, a status index and the FK for the confirming user.

Then a **hand-written backfill**: every pre-existing order is closed as `Received` with
`ReceivedAtUtc = CreatedAtUtc` and `ReceivedByEmployeeNumber` left NULL. Without it those orders
would default to `PendingArrival` and a Business Manager could "confirm" goods that the old
`/receive` path had already counted — a second receipt for one delivery. NULL is the honest value:
nobody confirmed them under this workflow. Verified on the dev database — 9 orders backfilled.

## 8. Tests actually run

- `dotnet test Project.slnx` — **167/167** (54 unit + 113 integration).
- `npx vitest run --pool=threads` — **137/137** across 23 files.
- Live against SQL Server and through the browser — see the 2026-09-04 entry in
  `AI_usage_report.md` for the full transcript.

New backend tests: `NewOrder_IsPendingArrival_AndStockIsUnchanged` ·
`ConfirmArrival_AsBusinessManager_MarksReceivedAndRaisesStockOnce` ·
`ConfirmArrival_Twice_Returns409_AndDoesNotRaiseStockAgain` ·
`ConfirmArrival_AsUnauthorisedRole_Returns403_AndStockIsUnchanged` (Theory: Manager, Engineer) ·
`ConfirmArrival_Unauthenticated_Returns401` · `ConfirmArrival_UnknownOrder_Returns404` ·
`InventoryTests.ReceiveEndpoint_IsGone_StockCannotBeRaisedAdHoc`.

## 9. Conflicts with existing project documents — for the team, not silently resolved

1. **`SupplierRequest` previously had no lifecycle on purpose.** Its own doc comment said no
   project document specified one and that inventing states was the K3 mistake — but also that
   "when the team specifies a lifecycle, it is an additive migration". This is that migration,
   on the team's instruction, using only the two states the task named. **The Plan (§3.3 table 11,
   §4.2) and `StationerySchema.sql` still describe no supplier-order lifecycle and no
   confirm-arrival endpoint; they now trail the code and should be updated.**
2. **Plan §4.2 lists `POST /api/v1/inventory/{itemId}/receive`.** It has been removed — see §3.
   That is a deliberate deviation from the Plan's endpoint catalogue.
3. `supplier-request-cart-implementation-handoff.md` says stock moves "later via `receiveGoods()`"
   — now stale.

## 10. Reviewer follow-ups

- Decide whether a `RowVersion` on `SupplierRequest` is wanted (§5).
- Partial deliveries are not modelled: an order is all-or-nothing. Nothing in the task or the Plan
  asks for per-line receipt; say so if it is wanted.
- There is still no route/nav entry for supplier orders — the list lives in a modal on the
  Inventory page, which is the smallest change that gives the Business Manager the action.
