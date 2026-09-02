/**
 * Derives a running balance for the stock ledger view.
 *
 * WHY THIS IS DERIVED AND NOT FETCHED: `StockTransactions` is the append-only source of truth
 * and `StationeryItems.QuantityAvailable` is a cached balance (CLAUDE.md principle #5). Neither
 * the table nor `StockTransactionDto` stores a per-row balance, so the only way to show one is
 * to walk the ledger. `GET /inventory/{itemId}/transactions` returns every row for the item
 * (`StockQueries.GetHistoryAsync` has no paging), newest first, so the walk is complete.
 *
 * The arithmetic is deliberately trivial and inspectable: the newest row's closing balance is
 * the item's current `quantityAvailable`, and each older row's closing balance is the one after
 * it minus that later row's change.
 */

/**
 * @param {Array} transactions newest-first, as returned by the API
 * @param {number} currentBalance the item's cached `quantityAvailable`
 * @returns {{rows: Array, openingBalance: number}} rows carry `balanceAfter`; `openingBalance`
 *   is what the ledger implies the item held before its first ever transaction. That should be
 *   0 — a non-zero value means the ledger and the cached balance disagree, which the UI surfaces
 *   rather than hides, since silently rendering a wrong balance is worse than admitting drift.
 */
export function withRunningBalance(transactions, currentBalance) {
  if (!Array.isArray(transactions) || transactions.length === 0) {
    return { rows: [], openingBalance: 0 }
  }

  let balance = Number(currentBalance) || 0
  const rows = transactions.map((tx) => {
    const balanceAfter = balance
    balance -= Number(tx.changeQuantity) || 0
    return { ...tx, balanceAfter }
  })

  return { rows, openingBalance: balance }
}

/** Signed display for a ledger change: Receipts read `+12`, Issues `−12` (a real minus sign). */
export function formatChange(changeQuantity) {
  const value = Number(changeQuantity) || 0
  return value < 0 ? `−${Math.abs(value)}` : `+${value}`
}
