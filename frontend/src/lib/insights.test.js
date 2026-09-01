import { describe, it, expect } from 'vitest'

import { buildInsightSentence, buildInventoryInsightSentence } from './insights.js'

describe('buildInsightSentence', () => {
  it('reports an empty period plainly', () => {
    expect(buildInsightSentence({ kind: 'Empty' })).toBe('No approved spend in this period yet.')
    expect(buildInsightSentence(null)).toBe('No approved spend in this period yet.')
  })

  it('describes a rise with its driver', () => {
    const text = buildInsightSentence({
      kind: 'PeriodDelta', scopeLabel: "Your team's", currentTotal: 1220, previousTotal: 1000,
      changePercent: 22, driverLabel: 'A4 Copy Paper',
    })
    expect(text).toBe("Your team's spend is up 22.0% versus the previous period, mostly driven by A4 Copy Paper.")
  })

  it('describes a fall without a driver clause', () => {
    const text = buildInsightSentence({
      kind: 'PeriodDelta', scopeLabel: 'Org', currentTotal: 800, previousTotal: 1000,
      changePercent: -20, driverLabel: 'Toner',
    })
    expect(text).toBe('Org spend is down 20.0% versus the previous period.')
  })

  it('handles no prior period to compare', () => {
    const text = buildInsightSentence({
      kind: 'PeriodDelta', scopeLabel: 'Your', currentTotal: 500, previousTotal: 0,
      changePercent: null, driverLabel: null,
    })
    expect(text).toContain('no prior period to compare yet')
  })

  it('builds a composition sentence for the headcount report', () => {
    const text = buildInsightSentence({
      kind: 'Composition', scopeLabel: "Your group's", driverLabel: 'A4 Copy Paper',
      driverSharePercent: 34.5, distinctRequestors: 6,
    })
    expect(text).toBe("A4 Copy Paper accounts for 34.5% of your group's spend across 6 requestors.")
  })
})

describe('buildInventoryInsightSentence', () => {
  it('flags reorder-level items', () => {
    const text = buildInventoryInsightSentence({ totalValue: 14520, itemsInStock: 7, itemsNeedingReorder: 2 })
    expect(text).toContain('2 items are at or below reorder level')
  })

  it('reads cleanly when nothing needs reordering', () => {
    const text = buildInventoryInsightSentence({ totalValue: 14520, itemsInStock: 7, itemsNeedingReorder: 0 })
    expect(text).toContain('none at or below reorder level')
  })
})
