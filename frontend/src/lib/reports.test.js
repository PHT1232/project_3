import { describe, it, expect } from 'vitest'

import {
  distributeTo100,
  filterLinesByRange,
  buildCostByItem,
  buildItemHeadcount,
  buildCumulativeCost,
  buildTeamExpenditure,
  resolveRangeFromPreset,
} from './reports.js'

/** Minimal fixture: 2 items, 2 requestors, one requestor appearing twice on item 1. */
const LINES = [
  { requestId: 1, requestorEmployeeNumber: 3, decidedAtUtc: '2026-06-10', itemId: 1, itemName: 'Paper', categoryName: 'Paper', quantity: 10, unitCostSnapshot: 6 },
  { requestId: 2, requestorEmployeeNumber: 3, decidedAtUtc: '2026-07-05', itemId: 1, itemName: 'Paper', categoryName: 'Paper', quantity: 5, unitCostSnapshot: 6 },
  { requestId: 3, requestorEmployeeNumber: 4, decidedAtUtc: '2026-07-20', itemId: 2, itemName: 'Pens', categoryName: 'Writing', quantity: 3, unitCostSnapshot: 12 },
]

describe('distributeTo100', () => {
  it('forces the shares to sum to exactly 100.00 (rounding residual on the largest)', () => {
    const pct = distributeTo100([1, 1, 1])
    expect(pct.reduce((a, b) => a + b, 0)).toBe(100)
    expect(Math.max(...pct)).toBe(33.34)
  })

  it('returns all zeros when the total is zero', () => {
    expect(distributeTo100([0, 0])).toEqual([0, 0])
  })

  it('gives a single value the whole 100', () => {
    expect(distributeTo100([42])).toEqual([100])
  })
})

describe('filterLinesByRange', () => {
  it('is inclusive of both endpoints', () => {
    const kept = filterLinesByRange(LINES, '2026-07-05', '2026-07-20')
    expect(kept.map((l) => l.requestId)).toEqual([2, 3])
  })
})

describe('buildCostByItem', () => {
  it('sorts by cost desc, sums percentages to 100.00, and reconciles the total', () => {
    const report = buildCostByItem(LINES)
    expect(report.rows.map((r) => r.itemId)).toEqual([1, 2]) // 90 vs 36
    expect(report.rows[0].approvedCost).toBe(90)
    expect(report.totalApprovedCost).toBe(126)
    expect(report.rows.reduce((a, r) => a + r.percentOfTotal, 0)).toBe(100)
  })
})

describe('buildItemHeadcount', () => {
  it('counts distinct requestors, not request rows (TC-17)', () => {
    const report = buildItemHeadcount(LINES)
    const paper = report.rows.find((r) => r.itemId === 1)
    expect(paper.requestCount).toBe(2)
    expect(paper.requestorCount).toBe(1)
  })

  it('sums the units approved per item', () => {
    const report = buildItemHeadcount(LINES)
    expect(report.rows.find((r) => r.itemId === 1).unitsApproved).toBe(15) // 10 + 5
    expect(report.rows.find((r) => r.itemId === 2).unitsApproved).toBe(3)
  })
})

describe('buildCumulativeCost', () => {
  it('is monotonically non-decreasing and ends at the grand total', () => {
    const report = buildCumulativeCost(LINES)
    const cumulative = report.points.map((p) => p.cumulativeCost)
    expect(cumulative).toEqual([...cumulative].sort((a, b) => a - b))
    expect(report.points.at(-1).cumulativeCost).toBe(report.totalApprovedCost)
    expect(report.totalApprovedCost).toBe(buildCostByItem(LINES).totalApprovedCost)
  })

  it('returns topConsumed ranked by units approved', () => {
    const report = buildCumulativeCost(LINES)
    expect(report.topConsumed.map((r) => r.itemName)).toEqual(['Paper', 'Pens']) // 15 vs 3 units
    expect(report.topConsumed[0]).toMatchObject({ unitsApproved: 15, approvedCost: 90 })
    expect(report.topConsumed.length).toBeLessThanOrEqual(5)
  })
})

describe('buildTeamExpenditure', () => {
  const TEAM_MAP = { 3: 'Alice (Mgr)', 4: 'Bob (Mgr)' }

  it('groups spend by team, counts distinct members, and sums shares to 100.00', () => {
    const report = buildTeamExpenditure(LINES, TEAM_MAP)
    expect(report.rows.map((r) => r.teamName)).toEqual(['Alice (Mgr)', 'Bob (Mgr)']) // 90 vs 36
    const alice = report.rows.find((r) => r.teamName === 'Alice (Mgr)')
    expect(alice).toMatchObject({ approvedCost: 90, memberCount: 1, requestCount: 2 })
    expect(report.totalApprovedCost).toBe(126)
    expect(report.rows.reduce((a, r) => a + r.percentOfTotal, 0)).toBe(100)
  })

  it('buckets requestors missing from the map under "Unassigned"', () => {
    const report = buildTeamExpenditure(LINES, { 3: 'Alice (Mgr)' })
    expect(report.rows.some((r) => r.teamName === 'Unassigned')).toBe(true)
  })
})

describe('resolveRangeFromPreset', () => {
  const bounds = { fromDate: '2026-04-30', toDate: '2026-08-28' }

  it('clamps the start to the data floor', () => {
    expect(resolveRangeFromPreset(365, bounds)).toEqual(bounds)
  })

  it('counts back from the latest data point', () => {
    expect(resolveRangeFromPreset(30, bounds)).toEqual({ fromDate: '2026-07-29', toDate: '2026-08-28' })
  })

  it('treats null as all-time', () => {
    expect(resolveRangeFromPreset(null, bounds)).toEqual(bounds)
  })
})
