import {
  MOCK_APPROVED_REQUEST_LINES,
  MOCK_DATA_BOUNDS,
  MOCK_TEAM_MAP,
} from './mock/reports.mock.js'
import {
  filterLinesByRange,
  buildCostByItem,
  buildItemHeadcount,
  buildCumulativeCost,
  buildTeamExpenditure,
} from '../lib/reports.js'

/**
 * Reports data access. Components import from here and never touch the data source directly.
 *
 * ---------------------------------------------------------------------------
 * EXPECTED BACKEND CONTRACT (Plan §4.2 / §5, page-map §9 — M3's work, not implemented yet)
 *
 *   All three are auth: Manager+ (RankLevel >= 2; Engineer -> 403), take
 *   ?fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD, and count APPROVED requests only
 *   (Plan [ASK] #6 default: money is committed at approval). Cost is
 *   SUM(RequestItems.Quantity * RequestItems.UnitCostSnapshot) — the snapshot,
 *   never the live StationeryItems.UnitCost. Aggregation is SQL-side GROUP BY.
 *
 *   GET /api/v1/reports/cost-by-item
 *     200 : { range, totalApprovedCost, rows: CostByItemRow[] }   rows sorted by cost desc
 *     CostByItemRow { itemId, itemName, categoryName, approvedCost, percentOfTotal }
 *     percentOfTotal values sum to exactly 100.00 (largest = 100 - Σ others; TC-16).
 *
 *   GET /api/v1/reports/item-headcount
 *     200 : { range, totalApprovedCost, rows: ItemHeadcountRow[] }
 *     ItemHeadcountRow { itemId, itemName, categoryName, approvedCost, requestorCount, requestCount }
 *     requestorCount = COUNT(DISTINCT RequestorEmployeeNumber) (TC-17), server-derived.
 *
 *   GET /api/v1/reports/cumulative-cost
 *     200 : { range, totalApprovedCost, points: CumulativePoint[], topConsumed: TopConsumed[] }
 *     CumulativePoint { periodKey: 'YYYY-MM', periodLabel, periodCost, cumulativeCost }
 *     TopConsumed    { itemName, categoryName, unitsApproved, approvedCost }  (top 5 by units)
 *     cumulativeCost is monotonically non-decreasing; the last one === totalApprovedCost.
 *
 *   GET /api/v1/reports/cost-by-team
 *     200 : { range, totalApprovedCost, rows: TeamRow[] }   rows sorted by cost desc
 *     TeamRow { teamName, memberCount, requestCount, approvedCost, percentOfTotal }
 *     Groups approved spend by the requestor's approving manager
 *     (Requests.ApproverEmployeeNumber → Users). percentOfTotal sums to exactly 100.00.
 *
 *   range = { fromDate, toDate } echoed back.
 *
 * TO GO LIVE: replace each function body with the `client` call shown, and delete
 * `./mock/reports.mock.js` + the `../lib/reports.js` import above (the server does the
 * aggregation then). No component changes are required.
 * ---------------------------------------------------------------------------
 */

// import client from './client.js'

/** The window the mock data covers — the date picker clamps its presets/inputs to this. */
export { MOCK_DATA_BOUNDS as REPORT_DATA_BOUNDS }

/**
 * Each payload carries a `kind` so the page can tell whether the data in hand
 * belongs to the tab currently selected — a tab switch changes `kind` before the
 * refetch resolves, and rendering a cost-by-item table against cumulative data
 * (or vice versa) is what turns the screen white.
 */

export async function getCostByItemReport({ fromDate, toDate }) {
  // return (await client.get('/reports/cost-by-item', { params: { fromDate, toDate } })).data
  const lines = filterLinesByRange(MOCK_APPROVED_REQUEST_LINES, fromDate, toDate)
  return { kind: 'COST_BY_ITEM', range: { fromDate, toDate }, ...buildCostByItem(lines) }
}

export async function getItemHeadcountReport({ fromDate, toDate }) {
  // return (await client.get('/reports/item-headcount', { params: { fromDate, toDate } })).data
  const lines = filterLinesByRange(MOCK_APPROVED_REQUEST_LINES, fromDate, toDate)
  return { kind: 'HEADCOUNT', range: { fromDate, toDate }, ...buildItemHeadcount(lines) }
}

export async function getCumulativeCostReport({ fromDate, toDate }) {
  // return (await client.get('/reports/cumulative-cost', { params: { fromDate, toDate } })).data
  const lines = filterLinesByRange(MOCK_APPROVED_REQUEST_LINES, fromDate, toDate)
  return { kind: 'CUMULATIVE', range: { fromDate, toDate }, ...buildCumulativeCost(lines) }
}

export async function getTeamExpenditureReport({ fromDate, toDate }) {
  // return (await client.get('/reports/cost-by-team', { params: { fromDate, toDate } })).data
  const lines = filterLinesByRange(MOCK_APPROVED_REQUEST_LINES, fromDate, toDate)
  return { kind: 'BY_TEAM', range: { fromDate, toDate }, ...buildTeamExpenditure(lines, MOCK_TEAM_MAP) }
}
