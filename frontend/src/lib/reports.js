/**
 * Cost-report aggregations — the client-side stand-in for what M3's SQL `GROUP BY`
 * queries will return once `GET /api/v1/reports/*` exists (Plan §4.2 / §5, page-map §9).
 *
 * Kept here (not in a component) so the maths is pure and unit-tested, and so both the
 * mock API layer (`src/api/reports.js`) and any client-side re-filtering use one
 * implementation. When the real endpoints land these stay only as a reference for the
 * expected shape.
 *
 * All figures are over **Approved** requests only and use `RequestItems.UnitCostSnapshot`
 * frozen at submission — never the live `StationeryItems.UnitCost` (Plan §3.4, page-map §9).
 */

const round2 = (n) => Math.round((n + Number.EPSILON) * 100) / 100

/** Line cost = quantity × the cost snapshotted on the request line. */
export function lineCost(line) {
  return line.quantity * line.unitCostSnapshot
}

/**
 * Keep only lines whose decision date falls within [fromDate, toDate] inclusive.
 * Dates are compared as `YYYY-MM-DD` strings, which orders correctly for ISO dates.
 */
export function filterLinesByRange(lines, fromDate, toDate) {
  return lines.filter((line) => {
    const day = line.decidedAtUtc.slice(0, 10)
    return day >= fromDate && day <= toDate
  })
}

/**
 * Turn a list of amounts into percentages that sum to **exactly 100.00**.
 * The rounding residual is absorbed into the largest share, computed as
 * `100 − Σ(others)` (page-map §9 / TC-16), so the column always reconciles.
 */
export function distributeTo100(amounts) {
  const total = amounts.reduce((sum, n) => sum + n, 0)
  if (total <= 0) return amounts.map(() => 0)

  const raw = amounts.map((n) => (n / total) * 100)
  const pct = raw.map((n) => round2(n))

  let largest = 0
  for (let i = 1; i < raw.length; i += 1) {
    if (raw[i] > raw[largest]) largest = i
  }
  const others = pct.reduce((sum, n, i) => (i === largest ? sum : sum + n), 0)
  pct[largest] = round2(100 - others)
  return pct
}

/** Report 1 — approved cost per item and each item's share of the total. */
export function buildCostByItem(lines) {
  const byItem = new Map()
  for (const line of lines) {
    const row = byItem.get(line.itemId) ?? {
      itemId: line.itemId,
      itemName: line.itemName,
      categoryName: line.categoryName,
      approvedCost: 0,
    }
    row.approvedCost += lineCost(line)
    byItem.set(line.itemId, row)
  }

  const rows = [...byItem.values()].sort((a, b) => b.approvedCost - a.approvedCost)
  const percentages = distributeTo100(rows.map((row) => row.approvedCost))

  return {
    totalApprovedCost: round2(rows.reduce((sum, row) => sum + row.approvedCost, 0)),
    rows: rows.map((row, i) => ({
      ...row,
      approvedCost: round2(row.approvedCost),
      percentOfTotal: percentages[i],
    })),
  }
}

/** Report 2 — approved cost plus the count of DISTINCT requestors per item (TC-17). */
export function buildItemHeadcount(lines) {
  const byItem = new Map()
  for (const line of lines) {
    let row = byItem.get(line.itemId)
    if (!row) {
      row = {
        itemId: line.itemId,
        itemName: line.itemName,
        categoryName: line.categoryName,
        approvedCost: 0,
        unitsApproved: 0,
        requestors: new Set(),
        requests: new Set(),
      }
      byItem.set(line.itemId, row)
    }
    row.approvedCost += lineCost(line)
    row.unitsApproved += line.quantity
    row.requestors.add(line.requestorEmployeeNumber)
    row.requests.add(line.requestId)
  }

  const rows = [...byItem.values()]
    .map(({ requestors, requests, approvedCost, ...rest }) => ({
      ...rest,
      approvedCost: round2(approvedCost),
      requestorCount: requestors.size,
      requestCount: requests.size,
    }))
    .sort((a, b) => b.approvedCost - a.approvedCost)

  return {
    totalApprovedCost: round2(rows.reduce((sum, row) => sum + row.approvedCost, 0)),
    rows,
  }
}

function monthLabel(key) {
  const [year, month] = key.split('-')
  return new Date(Date.UTC(Number(year), Number(month) - 1, 1)).toLocaleString('en-US', {
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  })
}

/**
 * Report 3 — approved cost per calendar month and the running cumulative total,
 * plus `topConsumed`: the 5 items with the most units approved in the period
 * (the "Top Consumed Items" section on the Cumulative tab).
 */
export function buildCumulativeCost(lines) {
  const byMonth = new Map()
  for (const line of lines) {
    const key = line.decidedAtUtc.slice(0, 7)
    byMonth.set(key, (byMonth.get(key) ?? 0) + lineCost(line))
  }

  let running = 0
  const points = [...byMonth.keys()]
    .sort()
    .map((key) => {
      const periodCost = round2(byMonth.get(key))
      running = round2(running + periodCost)
      return {
        periodKey: key,
        periodLabel: monthLabel(key),
        periodCost,
        cumulativeCost: running,
      }
    })

  const byItem = new Map()
  for (const line of lines) {
    const row = byItem.get(line.itemId) ?? {
      itemName: line.itemName,
      categoryName: line.categoryName,
      unitsApproved: 0,
      approvedCost: 0,
    }
    row.unitsApproved += line.quantity
    row.approvedCost += lineCost(line)
    byItem.set(line.itemId, row)
  }
  const topConsumed = [...byItem.values()]
    .map((row) => ({ ...row, approvedCost: round2(row.approvedCost) }))
    .sort((a, b) => b.unitsApproved - a.unitsApproved)
    .slice(0, 5)

  return { totalApprovedCost: running, points, topConsumed }
}

/**
 * Report 5 — approved spend grouped by the requestor's team (their approving manager).
 * `teamMap` is `{ requestorEmployeeNumber: teamName }`; lines whose requestor is not in
 * the map are grouped under "Unassigned". Shares sum to exactly 100.00 (see `distributeTo100`).
 */
export function buildTeamExpenditure(lines, teamMap) {
  const byTeam = new Map()
  for (const line of lines) {
    const teamName = teamMap[line.requestorEmployeeNumber] ?? 'Unassigned'
    let row = byTeam.get(teamName)
    if (!row) {
      row = { teamName, approvedCost: 0, members: new Set(), requests: new Set() }
      byTeam.set(teamName, row)
    }
    row.approvedCost += lineCost(line)
    row.members.add(line.requestorEmployeeNumber)
    row.requests.add(line.requestId)
  }

  const rows = [...byTeam.values()]
    .map(({ members, requests, approvedCost, teamName }) => ({
      teamName,
      approvedCost: round2(approvedCost),
      memberCount: members.size,
      requestCount: requests.size,
    }))
    .sort((a, b) => b.approvedCost - a.approvedCost)

  const percentages = distributeTo100(rows.map((row) => row.approvedCost))

  return {
    totalApprovedCost: round2(rows.reduce((sum, row) => sum + row.approvedCost, 0)),
    rows: rows.map((row, i) => ({ ...row, percentOfTotal: percentages[i] })),
  }
}

function isoMinusDays(iso, days) {
  const date = new Date(`${iso}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() - days)
  return date.toISOString().slice(0, 10)
}

/**
 * Resolve a preset (in days back from the latest data point) to a concrete
 * `{ fromDate, toDate }`, clamped to the range the data actually covers.
 * `days == null` means "all time".
 */
export function resolveRangeFromPreset(days, bounds) {
  if (days == null) return { fromDate: bounds.fromDate, toDate: bounds.toDate }
  const candidate = isoMinusDays(bounds.toDate, days)
  return {
    fromDate: candidate < bounds.fromDate ? bounds.fromDate : candidate,
    toDate: bounds.toDate,
  }
}
