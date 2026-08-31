import { describe, it, expect } from 'vitest'

import {
  ANY,
  DEFAULT_REPORT_FILTERS,
  isDefaultReportFilters,
  categoryOptions,
  applyReportFilters,
} from './reportFilters.js'

const ROWS = [
  { itemId: 1, itemName: 'A4 Copy Paper', categoryName: 'Paper', approvedCost: 780 },
  { itemId: 2, itemName: 'Blue Ballpoint Pens', categoryName: 'Writing', approvedCost: 1421 },
  { itemId: 3, itemName: '27-inch Monitor', categoryName: 'Tech', approvedCost: 1920 },
  { itemId: 4, itemName: 'Sticky Notes', categoryName: 'Paper', approvedCost: 64 },
]

describe('isDefaultReportFilters', () => {
  it('is true for the default and false once anything is set', () => {
    expect(isDefaultReportFilters(DEFAULT_REPORT_FILTERS)).toBe(true)
    expect(isDefaultReportFilters({ ...DEFAULT_REPORT_FILTERS, search: 'pen' })).toBe(false)
  })
})

describe('categoryOptions', () => {
  it('returns the distinct category names, sorted', () => {
    expect(categoryOptions(ROWS)).toEqual(['Paper', 'Tech', 'Writing'])
  })
})

describe('applyReportFilters', () => {
  it('passes everything through with default filters', () => {
    expect(applyReportFilters(ROWS, DEFAULT_REPORT_FILTERS)).toHaveLength(4)
  })

  it('matches the search term case-insensitively on the item name', () => {
    const out = applyReportFilters(ROWS, { ...DEFAULT_REPORT_FILTERS, search: 'PAPER' })
    expect(out.map((r) => r.itemId)).toEqual([1])
  })

  it('filters by category', () => {
    const out = applyReportFilters(ROWS, { ...DEFAULT_REPORT_FILTERS, category: 'Paper' })
    expect(out.map((r) => r.itemId)).toEqual([1, 4])
  })

  it('filters by approved-cost band', () => {
    const under100 = applyReportFilters(ROWS, { ...DEFAULT_REPORT_FILTERS, costBucket: 'LT_100' })
    expect(under100.map((r) => r.itemId)).toEqual([4])

    const over1000 = applyReportFilters(ROWS, { ...DEFAULT_REPORT_FILTERS, costBucket: 'GTE_1000' })
    expect(over1000.map((r) => r.itemId)).toEqual([2, 3])
  })

  it('combines all three filters', () => {
    const out = applyReportFilters(ROWS, { search: 'inch', category: 'Tech', costBucket: 'GTE_1000' })
    expect(out.map((r) => r.itemId)).toEqual([3])
  })

  it('treats ANY as no constraint', () => {
    expect(applyReportFilters(ROWS, { search: '', category: ANY, costBucket: ANY })).toHaveLength(4)
  })
})
