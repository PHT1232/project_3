import client from './client.js'

/**
 * Reports data access. Components import from here and never touch the data source directly.
 *
 * Backend: WebApi/Controllers/ReportsController.cs + Infrastructure/Queries/ReportQueries.cs.
 * `[Authorize]` only (not Manager+) — a plain requestor still gets their own spend and the
 * "My Requests" tab; row-level scoping happens server-side (see ReportQueries's doc comment)
 * and is keyed off the caller's JWT, never a client-supplied parameter. All endpoints take
 * `?fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD` and count committed spend only (Approved /
 * PartiallyApproved / Fulfilled requests), using RequestItems.LineTotal — the cost snapshot,
 * never the live catalogue price.
 *
 * Each payload carries a client-added `kind` so the page can tell whether the data in hand
 * belongs to the tab currently selected — a tab switch changes `kind` before the refetch
 * resolves, and rendering one report's shape against another's is what turns the screen white.
 */

export async function getCostByItemReport({ fromDate, toDate }) {
  const { data } = await client.get('/reports/cost-by-item', { params: { fromDate, toDate } })
  return { kind: 'COST_BY_ITEM', ...data }
}

export async function getItemHeadcountReport({ fromDate, toDate }) {
  const { data } = await client.get('/reports/item-headcount', { params: { fromDate, toDate } })
  return { kind: 'HEADCOUNT', ...data }
}

export async function getCumulativeCostReport({ fromDate, toDate }) {
  const { data } = await client.get('/reports/cumulative-cost', { params: { fromDate, toDate } })
  return { kind: 'CUMULATIVE', ...data }
}

export async function getTeamExpenditureReport({ fromDate, toDate }) {
  const { data } = await client.get('/reports/by-team', { params: { fromDate, toDate } })
  return { kind: 'BY_TEAM', ...data }
}

export async function getMyActivityReport({ fromDate, toDate }) {
  const { data } = await client.get('/reports/my-activity', { params: { fromDate, toDate } })
  return { kind: 'MY_REQUESTS', ...data }
}
