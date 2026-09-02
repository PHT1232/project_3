# Inventory: Stock Ledger History + Toolbar Selection Fix — Implementation Handoff

> Implemented 2026-09-03 on `feat/M2-inventory-ledger-history`, branched off `main` @ `6a80f9a`.
> Frontend only — **no backend, DTO, entity, endpoint or migration change**. The API this
> consumes (`GET /api/v1/inventory/{itemId}/transactions`) shipped with M2 and was already
> integration-tested; it simply had no caller.

## Why this work

Two gaps found while auditing catalogue/inventory for "built but not connected end-to-end":

1. **The stock ledger had no UI at all.** `StockTransactions` is the project's source of truth
   and `QuantityAvailable` only a cached balance (CLAUDE.md principle #5) — but every screen
   showed the cache and nothing showed the ledger. `getTransactionHistory` existed in
   `api/inventory.js` with **zero component consumers**.
2. **The toolbar's Adjust Stock / Receive Goods acted on the wrong item.** Both passed
   `visibleRows[0]` — whatever sorted first — so re-sorting or filtering silently changed which
   item the button would modify, and the checkbox the user had ticked was ignored. Both buttons
   write to the ledger, so this produced real, wrong rows in an append-only table.

## What changed

### 1. Toolbar selection fix (`InventoryPage.jsx`)

Both toolbar actions now operate on the **selected** row and require **exactly one** selection.

The "exactly one" rule is not arbitrary: `selectedIds` is shared with the multi-item supplier
cart, so with two or more ticked there is no unambiguous single target. Rather than guess, the
buttons disable and carry a `title` saying why ("Select one item to adjust or receive stock" /
"Select exactly one item — several are selected"). Row-level actions in the kebab menu were
already correct and are untouched.

### 2. Stock ledger history (new)

- **`pages/inventory/stockHistory.js`** — two pure functions, unit-tested in isolation:
  - `withRunningBalance(transactions, currentBalance)` derives a per-row closing balance.
  - `formatChange(qty)` renders `+12` / `−12`.
- **`components/StockHistoryModal.jsx`** — the read view. Fetch-on-open follows the existing
  `SubordinatesModal` precedent (`open && item ? fetch : Promise.resolve([])`), so nothing is
  requested until a user actually opens it. Full loading / error / empty states (CLAUDE.md §5).
- **`InventoryTable.jsx`** — a "View stock history" entry in the existing row menu.
- **`components/ui/Modal.jsx`** (SHARED) — additive `size` prop, default `md`. See below.

## Three decisions worth knowing

**1. The running balance is derived, not fetched — and that is a deliberate trade-off.**
Neither `StockTransactions` nor `StockTransactionDto` stores a per-row balance. The endpoint
returns *every* row for the item (`StockQueries.GetHistoryAsync` has no paging), newest first,
so the walk back from the current balance is complete and the arithmetic is trivial: the newest
row's closing balance is the item's `quantityAvailable`, and each older row's is the one after
it minus that later row's change. A reviewer can verify it on a whiteboard in ten seconds,
which is the standard CLAUDE.md §5 sets.

**2. Drift is surfaced, not hidden.** Because the balance is derived from the *cache* while the
rows come from the *ledger*, walking all the way back must land on 0. If it does not, the two
have disagreed and the derived column is untrustworthy — so the modal says so in place of
quietly rendering a wrong number. On the seeded database this fires for no item: the oldest row
of every ledger reconciles exactly (verified live, see below). Treat a drift warning in the wild
as a real data-integrity bug worth investigating, not a UI glitch.

**3. `Modal` gained a `size` prop — a shared-file change, justified here.** At the default
`max-w-md`, the Balance column sat behind a horizontal scrollbar; a ledger whose balance is
hidden by default defeats the point of the view. `size` defaults to `md`, so all six existing
dialogs render byte-identically; only this modal opts into `lg`. The alternative — dropping the
unit-cost sub-line to squeeze four columns into 408px — traded away real ledger information for
the sake of not touching a shared file, which seemed the worse deal. If the team would rather
not widen `Modal`, reverting is a two-line change plus a narrower table.

## Files changed

**New:** `frontend/src/pages/inventory/stockHistory.js` ·
`frontend/src/pages/inventory/components/StockHistoryModal.jsx` ·
`frontend/src/pages/inventory/stockHistory.test.js` ·
`frontend/src/pages/inventory/StockHistory.test.jsx`

**Modified:** `frontend/src/pages/inventory/InventoryPage.jsx` (toolbar target + history state) ·
`frontend/src/pages/inventory/components/InventoryTable.jsx` (menu entry + prop) ·
`frontend/src/components/ui/Modal.jsx` (additive `size`)

**APIs / DB:** none added or changed.

## Tests actually run

- `npx vitest run --pool=threads` — **107/107 passed, 18 files** (90 pre-existing + 17 new:
  8 for the pure helpers, 9 covering the modal and the toolbar fix). Re-run after the `Modal.jsx`
  change, not just before it.
- `npm run build` — clean, 1707 modules.
- Backend was not touched, so `dotnet test` was not re-run for this change.
- **Note:** on this machine the default `npm test` (forks pool) hangs; use
  `npx vitest run --pool=threads`, as CLAUDE.md §1 already records.

New test coverage: derived balances, zero-opening reconciliation, drift detection, empty and
non-array payloads, signed formatting; and at component level — no fetch until opened, correct
`itemId` requested, rows/reference/actor rendered, running balance correct, drift warning shown,
empty state, error state, toolbar disabled at 0 and 2+ selections, and the regression itself
(dialog opens for the *selected* row, not the first visible one).

### Live verification against real data

Run against SQL Server Express (`.\SQLEXPRESS`, `StationeryManagementSystem.Dev`, 40 seeded
items with ~90 days of history), signed in as the bootstrap MD:

- "Adjustable Laptop Stand" → **25 movements**, newest `Aug 28 Issue −4 → 10`, oldest
  `May 30 Receipt OPENING +16 → 16`. The first-ever row's derived balance equals its own
  quantity, i.e. the implied opening balance is exactly **0** and no drift warning fired —
  independently confirming both the arithmetic and the ledger-vs-cache invariant on real rows.
- Toolbar regression: with "Ballpoint Pens, Box of 12" (third row) selected, the toolbar's
  Adjust Stock opened for **Ballpoint Pens** — the old code would have opened "A3 Copy Paper",
  the first visible row.

## Known gaps / explicitly out of scope

- **No pagination on the history view.** The endpoint returns every row for an item; at ~25 rows
  per item that is fine, and the running-balance walk actually *requires* the complete set. An
  item with thousands of movements would need server-side paging plus a stored balance, which is
  a backend change and a different piece of work.
- **Still no way to view supplier-order history** (`GET /supplier-requests` remains uncalled) —
  deliberately left alone, since that module is a flagged scope breach pending a keep/revert
  decision.
- **`GET /inventory/low-stock` is still uncalled** — the summary tile uses the count from
  `GET /inventory`. Separate, smaller gap.
- This change does **not** make approval move stock. `IStockService.IssueAsync` still has zero
  callers; that remains the largest inventory gap and needs design answers first (partial-approval
  semantics, whether `Approved` implies `Fulfilled`, whether cancellation restores stock).

## Reviewer follow-ups

1. Confirm the `Modal` `size` addition is acceptable, or ask for the narrower-table alternative.
2. Confirm "exactly one selection" is the wanted toolbar rule (vs. removing the toolbar buttons
   entirely, since the row menu already covers both actions).
3. CLAUDE.md §5 asks for two reviewers on stock/catalogue changes. This one is read-only with
   respect to stock — it writes nothing — but it touches the inventory surface, so the rule
   arguably still applies.
