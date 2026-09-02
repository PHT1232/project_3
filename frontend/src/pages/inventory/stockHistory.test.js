import { describe, it, expect } from 'vitest'

import { withRunningBalance, formatChange } from './stockHistory.js'

/** Newest-first, matching what `GET /inventory/{itemId}/transactions` returns. */
const LEDGER = [
  { transactionId: 3, txType: 'Issue', changeQuantity: -5 },
  { transactionId: 2, txType: 'Adjustment', changeQuantity: -2 },
  { transactionId: 1, txType: 'Receipt', changeQuantity: 50 },
]

describe('withRunningBalance', () => {
  it('walks back from the current balance, newest row first', () => {
    // 50 received, 2 adjusted away, 5 issued => current 43.
    const { rows } = withRunningBalance(LEDGER, 43)

    expect(rows.map((r) => r.balanceAfter)).toEqual([43, 48, 50])
  })

  it('reconciles to a zero opening balance when the ledger agrees with the cache', () => {
    const { openingBalance } = withRunningBalance(LEDGER, 43)

    expect(openingBalance).toBe(0)
  })

  it('reports drift when the cached balance disagrees with the ledger', () => {
    // Cached balance is 50 but the ledger only accounts for 43 — 7 units unexplained.
    const { openingBalance } = withRunningBalance(LEDGER, 50)

    expect(openingBalance).toBe(7)
  })

  it('preserves the original transaction fields', () => {
    const { rows } = withRunningBalance(LEDGER, 43)

    expect(rows[0]).toMatchObject({ transactionId: 3, txType: 'Issue', changeQuantity: -5 })
  })

  it('handles an empty ledger without inventing a balance', () => {
    expect(withRunningBalance([], 12)).toEqual({ rows: [], openingBalance: 0 })
  })

  it('tolerates a missing or non-array payload', () => {
    expect(withRunningBalance(undefined, 12)).toEqual({ rows: [], openingBalance: 0 })
    expect(withRunningBalance(null, 12)).toEqual({ rows: [], openingBalance: 0 })
  })
})

describe('formatChange', () => {
  it('signs receipts positively and issues negatively', () => {
    expect(formatChange(12)).toBe('+12')
    expect(formatChange(-12)).toBe('−12')
  })

  it('treats zero as a positive-signed value rather than blank', () => {
    // The DB forbids a zero-quantity ledger row (CK_StockTransactions_ChangeQuantity <> 0),
    // so this only guards the display against unexpected data.
    expect(formatChange(0)).toBe('+0')
  })
})
