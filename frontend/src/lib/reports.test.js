import { describe, it, expect } from 'vitest'

import { resolveRangeFromPreset, defaultReportBounds } from './reports.js'

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

describe('defaultReportBounds', () => {
  it('ends today and starts roughly two years back', () => {
    const bounds = defaultReportBounds()
    const today = new Date().toISOString().slice(0, 10)
    expect(bounds.toDate).toBe(today)
    expect(bounds.fromDate < bounds.toDate).toBe(true)
  })
})
