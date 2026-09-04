import { useEffect, useState } from 'react'
import { AlertCircle, PackageCheck } from 'lucide-react'

import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import Badge from '../../../components/ui/Badge.jsx'
import { ErrorState, EmptyState } from '../../../components/ui/StateBlock.jsx'
import { SkeletonTable } from '../../../components/ui/Skeleton.jsx'
import useAsync from '../../../hooks/useAsync.js'
import {
  confirmSupplierRequestArrival,
  getSupplierRequests,
  SUPPLIER_ORDER_STATUS,
} from '../../../api/supplierRequests.js'
import { formatCurrency, formatDate } from '../../../lib/format.js'

/**
 * Supplier orders and the "Goods Arrived" action.
 *
 * An order is raised Pending Arrival and moves no stock. Only a Business Manager (rank >= 3) can
 * confirm the delivery physically turned up, and that confirmation is what posts the stock
 * receipt — once. Managers see the list but no button; the server enforces it either way (403),
 * the hidden button is only UX (Plan §2.5).
 */
export default function SupplierOrdersModal({ open, canConfirm, onClose, onConfirmed }) {
  const [confirmingId, setConfirmingId] = useState(null)
  const [error, setError] = useState(null)

  const { data, error: loadError, loading, reload } = useAsync(
    () => (open ? getSupplierRequests({ pageSize: 50 }) : Promise.resolve(null)),
    [open],
  )

  useEffect(() => {
    if (open) setError(null)
  }, [open])

  if (!open) return null

  const orders = data?.items ?? []

  async function handleConfirm(order) {
    setConfirmingId(order.supplierRequestId)
    setError(null)
    try {
      await confirmSupplierRequestArrival(order.supplierRequestId)
      // Reload both this list and the inventory behind it — the balance has just changed.
      reload()
      onConfirmed?.()
    } catch (err) {
      setError(
        err.response?.data?.detail ??
          err.response?.data?.error ??
          err.message ??
          'Could not confirm arrival.',
      )
      reload()
    } finally {
      setConfirmingId(null)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Supplier Orders"
      footer={
        <Button variant="secondary" onClick={onClose}>
          Close
        </Button>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-muted">
          Ordering does not add stock. An order stays <strong>Pending Arrival</strong> until a
          Business Manager confirms the goods physically arrived — only then does the inventory
          balance go up.
        </p>

        {!canConfirm && (
          <div className="flex gap-2 rounded-md bg-surface-muted px-3 py-2 text-sm text-ink-muted">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <p>Only a Business Manager can confirm that goods have arrived.</p>
          </div>
        )}

        {error && (
          <div className="flex gap-2 rounded-md bg-status-dangerBg px-3 py-2 text-sm text-status-danger">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <p>{error}</p>
          </div>
        )}

        {loading && <SkeletonTable label="Loading supplier orders…" rows={4} columns={[3, 4, 2, 3]} />}

        {!loading && loadError && <ErrorState error={loadError} onRetry={reload} />}

        {!loading && !loadError && orders.length === 0 && (
          <EmptyState
            title="No supplier orders"
            description="Select items in the inventory table and use “Request from Suppliers” to raise one."
          />
        )}

        {!loading && !loadError && orders.length > 0 && (
          <div className="overflow-x-auto rounded-md border border-surface-border">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-surface-border bg-surface-muted text-xs uppercase tracking-wide text-ink-muted">
                  <th className="px-3 py-2 font-semibold">Order</th>
                  <th className="px-3 py-2 font-semibold">Supplier</th>
                  <th className="px-3 py-2 text-center font-semibold">Lines</th>
                  <th className="px-3 py-2 text-right font-semibold">Total</th>
                  <th className="px-3 py-2 font-semibold">Status</th>
                  <th className="px-3 py-2 text-right font-semibold">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-surface-border">
                {orders.map((order) => {
                  const pending = order.status === SUPPLIER_ORDER_STATUS.PENDING_ARRIVAL
                  return (
                    <tr key={order.supplierRequestId}>
                      <td className="px-3 py-2.5 font-mono text-ink">
                        #{order.supplierRequestId}
                        <span className="block text-xs text-ink-muted">
                          {formatDate(order.createdAtUtc)}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-ink">{order.supplierName}</td>
                      <td className="px-3 py-2.5 text-center text-ink-muted">
                        {order.items?.length ?? 0}
                      </td>
                      <td className="px-3 py-2.5 text-right font-mono text-ink">
                        {formatCurrency(order.totalCost)}
                      </td>
                      <td className="px-3 py-2.5">
                        {pending ? (
                          <Badge tone="muted">Pending Arrival</Badge>
                        ) : (
                          <>
                            <Badge tone="plain">Received</Badge>
                            <span className="mt-1 block text-xs text-ink-muted">
                              {order.receivedByName ? `by ${order.receivedByName}` : ''}
                              {order.receivedAtUtc ? ` · ${formatDate(order.receivedAtUtc)}` : ''}
                            </span>
                          </>
                        )}
                      </td>
                      <td className="px-3 py-2.5 text-right">
                        {pending && canConfirm && (
                          <Button
                            size="sm"
                            disabled={confirmingId !== null}
                            onClick={() => handleConfirm(order)}
                            aria-label={`Confirm arrival of supplier order #${order.supplierRequestId}`}
                          >
                            <PackageCheck className="h-4 w-4" aria-hidden="true" />
                            {confirmingId === order.supplierRequestId
                              ? 'Confirming…'
                              : 'Goods Arrived'}
                          </Button>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Modal>
  )
}
