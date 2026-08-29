/**
 * ============================ TEMPORARY MOCK DATA ============================
 * Stand-in for GET /api/v1/reports/{cost-by-item,item-headcount,cumulative-cost}
 * (Plan §4.2 / §5, page-map.md §9) — M3's backend work (SQL-side GROUP BY over
 * Approved requests), which does not exist yet.
 *
 * DELETE THIS FILE once the endpoints are live. Only `src/api/reports.js` imports it.
 *
 * Shape mirrors the ERD (docs/Diagrams/ERD_project.png) + StationerySchema.sql:
 *   Requests(Status = 'Approved', RequestorEmployeeNumber, DecidedAtUtc)
 *   RequestItems(ItemId, Quantity, UnitCostSnapshot)   <- cost source, never live UnitCost
 *   StationeryItems(ItemName) / Categories(Name)
 *
 * Item names and unit costs are taken from ./catalogue.mock.js so figures line up
 * across pages. The approved requests are generated once, deterministically (fixed
 * seed), so every render, test and demo shows identical numbers and the three
 * reports reconcile (Σ per-item cost === final cumulative total).
 * ============================================================================
 */

/** Pinned so generated dates never drift; keep in step with the demo "today". */
const REPORT_TODAY = '2026-08-28'
const RANGE_DAYS = 120
const REQUEST_COUNT = 95
const SEED = 20260828

/** 15 items lifted from ./catalogue.mock.js (+ the toner from ./inventory.mock.js). */
const ITEMS = [
  { itemId: 7, itemName: 'Standard A4 Copy Paper, 500 Sheets', categoryName: 'Paper & Notebooks', unitCost: 6.4, tier: 'heavy' },
  { itemId: 9, itemName: 'Blue Ballpoint Pens, Box of 50', categoryName: 'Writing Instruments', unitCost: 11.75, tier: 'heavy' },
  { itemId: 110, itemName: 'Sticky Notes, 76x76mm, Pack of 12', categoryName: 'Paper & Notebooks', unitCost: 8.5, tier: 'heavy' },
  { itemId: 10, itemName: 'Highlighters, Assorted Colors, Pack of 4', categoryName: 'Writing Instruments', unitCost: 4.8, tier: 'heavy' },
  { itemId: 8, itemName: 'Spiral Bound Notebooks, A5, Ruled', categoryName: 'Paper & Notebooks', unitCost: 3.2, tier: 'medium' },
  { itemId: 11, itemName: 'Whiteboard Markers, Black, Box of 12', categoryName: 'Writing Instruments', unitCost: 9.9, tier: 'medium' },
  { itemId: 127, itemName: 'Desktop Stapler, Standard', categoryName: 'Organization', unitCost: 6.8, tier: 'medium' },
  { itemId: 6, itemName: 'USB Flash Drive 64GB', categoryName: 'Tech & Accessories', unitCost: 12.5, tier: 'medium' },
  { itemId: 12, itemName: 'Lever Arch Files, A4, Pack of 5', categoryName: 'Organization', unitCost: 14.25, tier: 'medium' },
  { itemId: 1, itemName: 'Ergonomic Wireless Mouse', categoryName: 'Tech & Accessories', unitCost: 24.99, tier: 'light' },
  { itemId: 13, itemName: 'Desk Organizer Tray, Mesh', categoryName: 'Organization', unitCost: 18.0, tier: 'light' },
  { itemId: 2, itemName: 'Mechanical Keyboard', categoryName: 'Tech & Accessories', unitCost: 89.0, tier: 'light' },
  { itemId: 15, itemName: 'Laser Printer Toner Cartridge, Black (High Yield)', categoryName: 'Tech & Accessories', unitCost: 98.0, tier: 'light' },
  { itemId: 3, itemName: 'USB-C Docking Station', categoryName: 'Tech & Accessories', unitCost: 145.5, tier: 'light' },
  { itemId: 5, itemName: '27-inch 4K Monitor', categoryName: 'Tech & Accessories', unitCost: 320.0, tier: 'light' },
]

/** Requestors — employee numbers in the 1–1000 range, names ≤ 15 chars (spec field rules). */
const REQUESTORS = [
  3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
]

/** Requested often → cheap consumables; rarely → hardware. Drives an interesting cost spread. */
const TIER_WEIGHT = { heavy: 6, medium: 3, light: 1 }
const TIER_MAX_QTY = { heavy: 12, medium: 6, light: 2 }

/** mulberry32 — small deterministic PRNG so the dataset is stable across runs. */
function mulberry32(seed) {
  let a = seed
  return () => {
    a |= 0
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function isoMinusDays(iso, days) {
  const date = new Date(`${iso}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() - days)
  return date.toISOString().slice(0, 10)
}

function generateApprovedLines() {
  const rand = mulberry32(SEED)

  const weighted = ITEMS.flatMap((item) => Array(TIER_WEIGHT[item.tier]).fill(item))
  const pickItem = () => weighted[Math.floor(rand() * weighted.length)]
  const pickFrom = (list) => list[Math.floor(rand() * list.length)]

  const lines = []
  for (let r = 1; r <= REQUEST_COUNT; r += 1) {
    const requestorEmployeeNumber = pickFrom(REQUESTORS)
    const decidedAtUtc = isoMinusDays(REPORT_TODAY, Math.floor(rand() * RANGE_DAYS))
    const lineCount = 1 + Math.floor(rand() * 3)

    const usedItemIds = new Set()
    for (let l = 0; l < lineCount; l += 1) {
      let item = pickItem()
      // Avoid the same item twice on one request — a real request would just raise the qty.
      let guard = 0
      while (usedItemIds.has(item.itemId) && guard < 8) {
        item = pickItem()
        guard += 1
      }
      if (usedItemIds.has(item.itemId)) continue
      usedItemIds.add(item.itemId)

      lines.push({
        requestId: r,
        requestorEmployeeNumber,
        decidedAtUtc,
        itemId: item.itemId,
        itemName: item.itemName,
        categoryName: item.categoryName,
        quantity: 1 + Math.floor(rand() * TIER_MAX_QTY[item.tier]),
        unitCostSnapshot: item.unitCost,
      })
    }
  }
  return lines
}

export const MOCK_APPROVED_REQUEST_LINES = Object.freeze(generateApprovedLines())

/** The window the mock data actually spans — the Reports date picker clamps to this. */
export const MOCK_DATA_BOUNDS = Object.freeze({
  fromDate: MOCK_APPROVED_REQUEST_LINES.reduce(
    (min, line) => (line.decidedAtUtc < min ? line.decidedAtUtc : min),
    REPORT_TODAY,
  ),
  toDate: REPORT_TODAY,
})

/**
 * Simulated team assignments (requestorEmployeeNumber → team / approving-manager name).
 * The mock lines carry `requestorEmployeeNumber` but no approver — in production the
 * "team" is derived from `Users.SuperiorEmployeeNumber` (the manager who approves the
 * request). This static map stands in for that join so the "By Team" report has data.
 * Keys are the employee numbers in `REQUESTORS` above.
 */
export const MOCK_TEAM_MAP = Object.freeze({
  3: 'Alice Chen (Mgr)',
  4: 'Alice Chen (Mgr)',
  5: 'Alice Chen (Mgr)',
  6: 'Bob Tan (Mgr)',
  7: 'Bob Tan (Mgr)',
  8: 'Bob Tan (Mgr)',
  9: 'Carol Lim (Mgr)',
  10: 'Carol Lim (Mgr)',
  11: 'Carol Lim (Mgr)',
  12: 'David Ng (Mgr)',
  13: 'David Ng (Mgr)',
  14: 'David Ng (Mgr)',
})
