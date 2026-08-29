import { useEffect, useState } from 'react'
import { AlertCircle, Trash2 } from 'lucide-react'

import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { createSupplierRequests } from '../../../api/supplierRequests.js'
import { getSuppliers } from '../../../api/suppliers.js'
import { formatCurrency } from '../../../lib/format.js'

/**
 * Review the inventory cart and submit it as supplier replenishment orders.
 *
 * Submitting creates orders only — it does NOT increase stock. Stock moves when the goods
 * actually arrive, via the row-level "Receive goods" action.
 *
 * Rows whose item has no preferred supplier show a supplier picker; the server rejects the whole
 * submission if any such row is left unchosen, so the submit button stays disabled until they are.
 */
const inputClass =
  'h-9 w-full rounded-md border border-surface-border bg-surface-card px-2 text-sm text-ink placeholder:text-ink-subtle'

export default function SupplierRequestModal({ open, rows, quantities, onQuantityChange, onRemove, onClose, onSuccess }) {
  const [suppliers, setSuppliers] = useState([])
  const [supplierChoice, setSupplierChoice] = useState({})
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState(null)
  const [result, setResult] = useState(null)

  const needsSupplier = rows.some((row) => !row.supplierId)

  useEffect(() => {
    if (!open) {
      setError(null)
      setResult(null)
      setSubmitting(false)
      setSupplierChoice({})
    }
  }, [open])

  // Only load the supplier list when a row actually needs one.
  useEffect(() => {
    let cancelled = false
    if (!open || !needsSupplier || suppliers.length > 0) return undefined

    getSuppliers({ pageSize: 200, includeInactive: false })
      .then((data) => {
        if (!cancelled) setSuppliers(data.items ?? [])
      })
      .catch(() => {
        if (!cancelled) setError(new Error('Could not load the supplier list. Close and try again.'))
      })

    return () => {
      cancelled = true
    }
  }, [open, needsSupplier, suppliers.length])

  if (!open) return null

  const unresolved = rows.filter((row) => !row.supplierId && !supplierChoice[row.itemId])
  const invalidQuantity = rows.some((row) => !(Number(quantities[row.itemId]) > 0))
  const canSubmit = rows.length > 0 && unresolved.length === 0 && !invalidQuantity && !submitting

  const estimatedTotal = rows.reduce(
    (sum, row) => sum + (Number(quantities[row.itemId]) || 0) * (row.unitCost ?? 0),
    0,
  )

  async function handleSubmit() {
    setSubmitting(true)
    setError(null)
    try {
      const payload = rows.map((row) => ({
        itemId: row.itemId,
        quantity: Number(quantities[row.itemId]),
        supplierId: row.supplierId ?? Number(supplierChoice[row.itemId]),
      }))
      const created = await createSupplierRequests(payload)
      setResult(created)
      onSuccess()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  if (result) {
    const itemCount = result.reduce((sum, request) => sum + request.items.length, 0)
    return (
      <Modal
        open
        onClose={onClose}
        title="Request submitted"
        footer={<Button onClick={onClose}>Done</Button>}
      >
        <p className="text-sm text-ink">
          {itemCount} item{itemCount === 1 ? '' : 's'} requested from {result.length} supplier
          {result.length === 1 ? '' : 's'}. Stock is unchanged until the goods are received.
        </p>
        <ul className="mt-4 space-y-3">
          {result.map((request) => (
            <li key={request.supplierRequestId} className="rounded-md border border-surface-border p-3">
              <p className="text-sm font-bold text-ink">
                {request.supplierName}{' '}
                <span className="font-normal text-ink-muted">
                  (#{request.supplierRequestId} · {formatCurrency(request.totalCost)})
                </span>
              </p>
              <ul className="mt-1 space-y-0.5">
                {request.items.map((line) => (
                  <li key={line.itemId} className="text-sm text-ink-muted">
                    {line.itemName} × {line.quantity}
                  </li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
      </Modal>
    )
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Review supplier request"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={!canSubmit}>
            {submitting ? 'Submitting…' : 'Submit request'}
          </Button>
        </>
      }
    >
      <p className="mb-3 text-sm text-ink-muted">
        Items are grouped by supplier when submitted. This raises an order — it does not change
        stock levels.
      </p>

      {error && (
        <div className="mb-3 flex gap-2 rounded-md bg-status-dangerBg px-3 py-2 text-sm text-status-danger">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <div>
            <p className="font-bold">Could not submit the request.</p>
            <ul className="mt-1 list-disc space-y-0.5 pl-4">
              {(error.response?.data?.errors?.items ?? [error.response?.data?.detail ?? error.message])
                .filter(Boolean)
                .map((message) => (
                  <li key={message}>{message}</li>
                ))}
            </ul>
          </div>
        </div>
      )}

      <div className="max-h-80 overflow-y-auto">
        <table className="w-full text-left text-sm">
          <thead className="text-xs uppercase tracking-wide text-ink-subtle">
            <tr>
              <th scope="col" className="pb-2">Item</th>
              <th scope="col" className="pb-2">Supplier</th>
              <th scope="col" className="pb-2 text-right">In stock</th>
              <th scope="col" className="pb-2 text-right">Quantity</th>
              <th scope="col" className="pb-2" />
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.itemId} className="border-t border-surface-border">
                <td className="py-2 pr-3 align-middle text-ink">{row.itemName}</td>
                <td className="py-2 pr-3 align-middle">
                  {row.supplierId ? (
                    <span className="text-ink-muted">{row.supplierName}</span>
                  ) : (
                    <select
                      aria-label={`Supplier for ${row.itemName}`}
                      className={inputClass}
                      value={supplierChoice[row.itemId] ?? ''}
                      onChange={(e) =>
                        setSupplierChoice((current) => ({ ...current, [row.itemId]: e.target.value }))
                      }
                    >
                      <option value="">Choose supplier…</option>
                      {suppliers.map((supplier) => (
                        <option key={supplier.supplierId} value={supplier.supplierId}>
                          {supplier.name}
                        </option>
                      ))}
                    </select>
                  )}
                </td>
                <td className="py-2 pr-3 text-right align-middle tabular-nums text-ink-muted">
                  {row.quantityAvailable}
                </td>
                <td className="py-2 pr-3 align-middle">
                  <input
                    type="number"
                    min="1"
                    aria-label={`Quantity for ${row.itemName}`}
                    className={`${inputClass} w-24 text-right tabular-nums`}
                    value={quantities[row.itemId] ?? ''}
                    onChange={(e) => onQuantityChange(row.itemId, e.target.value)}
                  />
                </td>
                <td className="py-2 align-middle">
                  <button
                    type="button"
                    onClick={() => onRemove(row.itemId)}
                    aria-label={`Remove ${row.itemName} from the request`}
                    className="rounded p-1 text-ink-muted hover:bg-surface-muted hover:text-status-danger"
                  >
                    <Trash2 className="h-4 w-4" aria-hidden="true" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="mt-3 text-right text-sm text-ink-muted">
        Estimated total <span className="font-bold text-ink">{formatCurrency(estimatedTotal)}</span>
      </p>

      {unresolved.length > 0 && (
        <p className="mt-2 text-sm text-ink-muted">
          Choose a supplier for {unresolved.length} item{unresolved.length === 1 ? '' : 's'} before
          submitting.
        </p>
      )}
    </Modal>
  )
}
