import { AlertCircle } from 'lucide-react'

import Modal from '../../../components/ui/Modal.jsx'
import Badge from '../../../components/ui/Badge.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../../components/ui/StateBlock.jsx'
import useAsync from '../../../hooks/useAsync.js'
import { getTransactionHistory } from '../../../api/inventory.js'
import { formatCurrency, formatDate, formatNumber } from '../../../lib/format.js'
import { withRunningBalance, formatChange } from '../stockHistory.js'

/**
 * Stock ledger history for one item — the read view over `StockTransactions`.
 *
 * This is the only place the append-only ledger is visible. Everywhere else in the app shows
 * `QuantityAvailable`, the cached balance; this shows the transactions that produced it
 * (CLAUDE.md principle #5, "stock is a ledger, not a counter").
 *
 * Fetch-on-open follows the `SubordinatesModal` precedent: the request is skipped entirely
 * while closed, and re-runs when a different item is opened.
 *
 * Unlike `SupplierRequestModal` / `RequestDetailModal`, this one opts into the wider `Modal`
 * size: at the default width the running-balance column sat behind a horizontal scrollbar, and
 * a ledger whose balance is hidden by default defeats the point of the view. The `size` prop is
 * additive and leaves every other dialog untouched.
 */

/** Ledger types come from `StockTransactionType` (Receipt / Issue / Adjustment). */
const TYPE_TONE = {
  Receipt: 'outline',
  Issue: 'muted',
  Adjustment: 'plain',
}

export default function StockHistoryModal({ open, item, onClose }) {
  const { data, error, loading, reload } = useAsync(
    () => (open && item ? getTransactionHistory(item.itemId) : Promise.resolve([])),
    [open, item?.itemId],
  )

  if (!item) return null

  const { rows, openingBalance } = withRunningBalance(data ?? [], item.quantityAvailable)

  return (
    <Modal open={open} onClose={onClose} size="lg" title={`Stock history — ${item.itemName}`}>
      {loading && <LoadingState label="Loading stock history…" />}

      {!loading && error && <ErrorState error={error} onRetry={reload} />}

      {!loading && !error && rows.length === 0 && (
        <EmptyState
          title="No stock movements"
          description="Nothing has been received, issued or adjusted for this item yet."
        />
      )}

      {!loading && !error && rows.length > 0 && (
        <>
          <p className="mb-3 text-xs text-ink-muted">
            Current balance{' '}
            <span className="font-semibold text-ink">
              {formatNumber(item.quantityAvailable)}
            </span>{' '}
            · {rows.length} movement{rows.length === 1 ? '' : 's'}, newest first
          </p>

          <div className="max-h-80 overflow-y-auto overflow-x-auto rounded-md border border-surface-border">
            <table className="w-full min-w-[420px] text-left text-sm">
              <thead>
                <tr className="border-b border-surface-border bg-surface-muted">
                  <th scope="col" className="px-3 py-2 font-semibold text-ink">When</th>
                  <th scope="col" className="px-3 py-2 font-semibold text-ink">Type</th>
                  <th scope="col" className="px-3 py-2 text-right font-semibold text-ink">
                    Change
                  </th>
                  <th scope="col" className="px-3 py-2 text-right font-semibold text-ink">
                    Balance
                  </th>
                </tr>
              </thead>
              <tbody>
                {rows.map((tx) => (
                  <tr
                    key={tx.transactionId}
                    className="border-b border-surface-border last:border-0 align-top"
                  >
                    <td className="px-3 py-2 text-ink">
                      {formatDate(tx.createdAtUtc)}
                      <span className="mt-0.5 block text-xs text-ink-muted">
                        by {tx.createdByName}
                      </span>
                    </td>
                    <td className="px-3 py-2">
                      <Badge tone={TYPE_TONE[tx.txType] ?? 'plain'}>{tx.txType}</Badge>
                      {tx.reference && (
                        <span className="mt-0.5 block text-xs text-ink-muted">{tx.reference}</span>
                      )}
                    </td>
                    <td
                      className={`px-3 py-2 text-right font-semibold ${
                        tx.changeQuantity < 0 ? 'text-status-danger' : 'text-ink'
                      }`}
                    >
                      {formatChange(tx.changeQuantity)}
                      <span className="mt-0.5 block text-xs font-normal text-ink-muted">
                        @ {formatCurrency(tx.unitCostSnapshot)}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right text-ink">
                      {formatNumber(tx.balanceAfter)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* The ledger is the source of truth; QuantityAvailable is a cache. Walking every
              movement back from the current balance must land on 0. If it does not, the two
              have drifted — say so rather than presenting the derived column as sound. */}
          {openingBalance !== 0 && (
            <div className="mt-3 flex gap-2 rounded-md bg-status-dangerBg px-3 py-2 text-xs text-status-danger">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <p>
                The ledger does not reconcile with the recorded balance (off by{' '}
                {formatNumber(openingBalance)}). The Balance column above is derived from the
                current stock level, so treat it as unreliable for this item and report it.
              </p>
            </div>
          )}
        </>
      )}
    </Modal>
  )
}
