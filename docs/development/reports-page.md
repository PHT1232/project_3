# Reports Page — Implementation Handoff

> Frontend only. Owner label: **M3 (SQL) + M2 (UI)** per page-map §9. Route `/reports`,
> Manager+ gated (`ProtectedRoute requireManager` + `navigation.js` `minRankLevel: 2`).
> Backend (`GET /api/v1/reports/*`) is **not built** — this page runs on mock data.

## 1. What the page does

Five tabs, each a distinct view (page-map §9: "three distinct views, not one page with
three columns" — extended here to five):

| Tab id | Label | Source | Date-filtered? |
|---|---|---|---|
| `COST_BY_ITEM` | Cost by Item | `getCostByItemReport` | yes |
| `HEADCOUNT` | Cost & Headcount | `getItemHeadcountReport` | yes |
| `CUMULATIVE` | Cumulative Cost | `getCumulativeCostReport` | yes |
| `INVENTORY_VALUATION` | Inventory Valuation | `getInventory` (inventory API) | **no — point-in-time** |
| `BY_TEAM` | By Team | `getTeamExpenditureReport` | yes |

All money figures are over **Approved requests only** and use `RequestItems.UnitCostSnapshot`
(never the live `StationeryItems.UnitCost`) — Plan §3.4 / page-map §9.

Cross-cutting UI:
- **ReportMetaBar** — document-style header strip: `Generated: … · Period: … · Approved
  requests only · Manager view`. On `INVENTORY_VALUATION` the period is replaced by
  `As of <today>`. "Generated" re-stamps every time the active report payload changes.
- **Export CSV / Print** buttons (top-right, next to the date control). Both are pure
  frontend. Export is disabled + tooltipped when loading / empty / filtered-to-nothing.
- **Sortable columns** on the four table views (all except the Cumulative monthly table):
  click cycles unsorted → desc → asc → unsorted, with a `↕ / ↓ / ↑` indicator.
- **Search + Category + Approved-cost filters** (`ReportToolbar`) on the two item tabs only.

## 2. Data flow

```
reports.mock.js  (deterministic seed: ~95 approved requests / ~178 lines, fixed PRNG+seed)
   │  MOCK_APPROVED_REQUEST_LINES, MOCK_DATA_BOUNDS, MOCK_TEAM_MAP
   ▼
lib/reports.js  (pure aggregations — unit-tested in lib/reports.test.js)
   │  buildCostByItem · buildItemHeadcount · buildCumulativeCost(+topConsumed) · buildTeamExpenditure
   │  filterLinesByRange · distributeTo100 · resolveRangeFromPreset
   ▼
api/reports.js  (mock fetchers; each returns a `kind` discriminator + `range`)
   ▼
ReportsPage.jsx  (two useAsync hooks: one for the date-range FETCHERS map, one for inventory
   │  that only fires when its tab is active; `kind` guard prevents stale cross-tab render)
   ▼
component views  (CostByItemView, ItemHeadcountView, CumulativeCostView,
                  InventoryValuationView, TeamExpenditureView)
```

**`kind` guard:** after a tab switch `useAsync` still holds the previous payload for one
render. `ReportsPage` computes `report = main.data?.kind === tab ? main.data : null` so the
wrong view never renders against the wrong shape (this was a real white-screen bug — see
`AI_usage_report.md` 2026-08-28).

**Client-side filtering** (`applyReportFilters`, item tabs): narrows `report.rows`; the table
footer then shows the shown subset, and the `% of Total` column keeps meaning "share of the
full period spend" (a note is shown when a filter is active). `distributeTo100` still
guarantees the unfiltered column sums to exactly 100.00 (TC-16).

**Sorting** (`tableSort.js` + `SortHeader.jsx`): each view holds its own `sort` state
(`{ key, dir } | null`); `applySort` reorders the already-filtered rows. Footer totals are
unaffected (sort changes order, not membership).

## 3. Backend contract this stands in for

Documented in full at the top of `frontend/src/api/reports.js`. Summary — all `Manager+`,
`?fromDate=&toDate=`, Approved-only, SQL-side `GROUP BY`:

| Route | Payload |
|---|---|
| `GET /api/v1/reports/cost-by-item` | `{ range, totalApprovedCost, rows: {itemId,itemName,categoryName,approvedCost,percentOfTotal}[] }` |
| `GET /api/v1/reports/item-headcount` | `rows: {…,approvedCost,unitsApproved,requestorCount,requestCount}` |
| `GET /api/v1/reports/cumulative-cost` | `{ …, points: {periodKey,periodLabel,periodCost,cumulativeCost}[], topConsumed: {itemName,categoryName,unitsApproved,approvedCost}[] }` |
| `GET /api/v1/reports/cost-by-team` | `rows: {teamName,memberCount,requestCount,approvedCost,percentOfTotal}` — group by `Requests.ApproverEmployeeNumber → Users` |

Inventory Valuation reuses `GET /api/v1/inventory` (`{ items, summary }`) — no new endpoint.

## 4. Files

**New**
- `frontend/src/lib/csvExport.js` — `toCsv(headers, rows)` + `exportToCsv(filename, headers, rows)` (RFC-4180 quoting, UTF-8 BOM, `<a>`-download).
- `frontend/src/pages/reports/tableSort.js` — `nextSort`, `sortIndicator`, `applySort`.
- `frontend/src/pages/reports/components/SortHeader.jsx` — sortable `<th>`.
- `frontend/src/pages/reports/components/ReportMetaBar.jsx`
- `frontend/src/pages/reports/components/InventoryValuationView.jsx`
- `frontend/src/pages/reports/components/TeamExpenditureView.jsx`

**Modified**
- `frontend/src/api/mock/reports.mock.js` — **added** `MOCK_TEAM_MAP` export only. Generator / PRNG / seed untouched.
- `frontend/src/lib/reports.js` — `buildTeamExpenditure(lines, teamMap)`; `buildCumulativeCost` now also returns `topConsumed`.
- `frontend/src/api/reports.js` — `getTeamExpenditureReport` + contract comment.
- `frontend/src/pages/reports/ReportsPage.jsx` — 2 new tabs, inventory branch (2nd `useAsync`), Export/Print buttons, `ReportMetaBar`, print `<style>` injection, `ITEM_TABS` set.
- `frontend/src/pages/reports/components/{CostByItemView,ItemHeadcountView}.jsx` — sortable columns.
- `frontend/src/pages/reports/components/CumulativeCostView.jsx` — "Top Consumed Items This Period" section.
- `frontend/src/lib/reports.test.js` — +3 cases (`topConsumed`, `buildTeamExpenditure` ×2).

**Untouched (per task):** `reportFilters.js`, `reportFilters.test.js`, and everything outside
`pages/reports/` · `lib/` · `api/`. (`api/mock/inventory.mock.js` and `catalogue.mock.js` were
later removed by the `origin/main` M2 merge when those pages were wired to the real API — see §9.)

## 5. Printing

`ReportsPage` injects a `<style id="reports-print-style">` into `<head>` on mount (removed on
unmount). `@media print`: `body * { visibility: hidden }` then `[data-print-region] *` visible
— this hides the sidebar and top header **without modifying those components** (they are out
of scope). `[data-print-hide]` (tabs, toolbar, date control, Export/Print buttons, charts)
is `display:none`; tables are forced black-on-white; a `[data-print-footer]` "HMT
Technologies … — Confidential" line prints at the bottom.

## 6. Tests actually run

- `npm run build` — pass (Vite, ~1690 modules, no errors/warnings).
- `npm test` — **37 passed** (6 files). New: `topConsumed` ranking, `buildTeamExpenditure`
  grouping + 100.00% shares + Unassigned bucket.
- `npx vitest run reportFilters` — 8 passed (unchanged).
- Node harness over the real data path: BY_TEAM 4 teams / shares = 100.00 / sorted desc;
  `topConsumed` top-5 by units; inventory valuation total $5,727.95, 7 in stock, 1 reorder;
  CSV escaping of `Lever Arch Files, A4, Pack of 5` and embedded quotes verified.
- **Not done:** no browser click-through by the developer; no component/render tests for the
  new views; print output not visually inspected.

## 7. Assumptions & known gaps

- **No Reports wireframe / Figma access** — layout follows the existing design system
  (Dashboard/Inventory). Needs a visual check by someone with Figma access.
- **Inventory Valuation has no Category column** — `InventoryRowDto` (the live
  `GET /api/v1/inventory`) carries no category field and parsing one from the item name
  would be fabricated data. Single "Item" column instead; revisit if the endpoint adds
  `categoryName`. (Documented in the component.)
- **Stock-status badge colours** — the brief asks for green/amber/red; the design-system
  `status` tokens are dark/grey/red, so this view uses Tailwind default `emerald/amber/red`
  utilities. Only place in the app that does this.
- **`MOCK_TEAM_MAP`** is a static stand-in for the `Users.SuperiorEmployeeNumber` join; team
  names are invented per the task spec. Requestors absent from the map bucket to "Unassigned".
- **`BY_TEAM` KPI tiles** (Approved Spend / Teams / Approved Requests) are not specified by
  the brief — chosen to match the other date-range tabs.
- **CSV for item tabs exports the filtered rows** (what's on screen), in report order, not
  the view's local sort order. Other tabs export the full report.
- `package-lock.json` shows 30 unrelated deletions from an earlier session's `npm install`;
  no package was added or removed by this task and `package.json` is unchanged.

## 8. Reviewer follow-ups

- Confirm the two new reports are wanted (`Inventory Valuation`, `By Team`) and their column
  sets; `By Team` depends on a `cost-by-team` endpoint that isn't in the Plan's §4.2 catalogue.
- Confirm the green/amber/red status badge deviation from the `status` design tokens.
- When the real endpoints land: delete `api/mock/reports.mock.js`, drop the `lib/reports.js`
  import from `api/reports.js`, and (for Inventory Valuation) add the Category column.

## 9. Post-merge with `origin/main` (team M2 work)

Branch `feat/M2-catalogue-cost-badge` merged `origin/main` (`b1e9f35`, the M2 catalogue /
suppliers / inventory backend + frontend wiring). Conflicts resolved in `App.jsx`,
`navigation.js`, `AI_usage_report.md` — both sides kept:

- Reports route stays in the `ProtectedRoute requireManager` group (ours); Inventory,
  Suppliers and the new `/catalogue/manage` Item Management route joined that same group
  (theirs). Reports nav item keeps `minRankLevel: 2`.
- The team wired Inventory to the real API and **deleted** `api/mock/inventory.mock.js` /
  `catalogue.mock.js`. The Inventory Valuation tab already calls `getInventory()` (not a
  mock import), so it now reads live `GET /api/v1/inventory` data — shape unchanged
  (`{ items, summary }`, `InventoryRowDto` rows). The four report tabs still use
  `reports.mock.js`.

Post-merge verification: `npm run build` pass (1692 modules); `npm test` **51 passed** (9
files — our 3 reports test files + the team's new suites).

