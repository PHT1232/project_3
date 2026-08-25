import { useEffect, useState } from 'react'
import { AlertCircle } from 'lucide-react'

import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { adjustStock, receiveGoods } from '../../../api/inventory.js'

/**
 * Adjust Stock / Receive Goods dialog.
 *
 * Fields mirror the documented request bodies in `src/api/inventory.js`:
 *   adjust  → { changeQuantity, reason }   — reason is REQUIRED (Plan §4.2)
 *   receive → { quantity, reference }
 *
 * Submission goes through the service layer, which currently rejects because the endpoints do
 * not exist yet. The failure is surfaced honestly instead of showing a fake success. When M3
 * ships the endpoints, only `src/api/inventory.js` changes.
 */
const CONFIG = {
  adjust: {
    title: 'Adjust Stock',
    submitLabel: 'Apply adjustment',
    quantityLabel: 'Change in quantity',
    quantityHint: 'Use a negative number to reduce stock.',
    submit: adjustStock,
  },
  receive: {
    title: 'Receive Goods',
    submitLabel: 'Receive goods',
    quantityLabel: 'Quantity received',
    quantityHint: 'Quantity delivered by the supplier.',
    submit: receiveGoods,
  },
}

const inputClass =
  'mt-1 h-10 w-full rounded-md border border-surface-border bg-surface-card px-3 text-sm text-ink placeholder:text-ink-subtle'

export default function StockActionModal({ mode, item, onClose }) {
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    setQuantity('')
    setReason('')
    setError(null)
    setSubmitting(false)
  }, [mode, item])

  if (!mode || !item) return null
  const config = CONFIG[mode]

  const quantityValue = Number(quantity)
  const quantityValid =
    quantity !== '' &&
    Number.isFinite(quantityValue) &&
    (mode === 'adjust' ? quantityValue !== 0 : quantityValue > 0)
  const reasonValid = mode !== 'adjust' || reason.trim().length > 0
  const canSubmit = quantityValid && reasonValid && !submitting

  async function handleSubmit(event) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await config.submit(item.itemId, { quantity: quantityValue, reason: reason.trim() })
      onClose()
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={config.title}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button form="stock-action-form" type="submit" disabled={!canSubmit}>
            {submitting ? 'Working…' : config.submitLabel}
          </Button>
        </>
      }
    >
      <form id="stock-action-form" onSubmit={handleSubmit} className="space-y-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Item</p>
          <p className="mt-1 text-sm font-semibold text-ink">{item.itemName}</p>
          <p className="font-mono text-xs text-ink-muted">{item.sku}</p>
        </div>

        <div>
          <label htmlFor="stock-quantity" className="text-sm font-medium text-ink">
            {config.quantityLabel}
          </label>
          <input
            id="stock-quantity"
            type="number"
            inputMode="numeric"
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
            className={inputClass}
          />
          <p className="mt-1 text-xs text-ink-muted">{config.quantityHint}</p>
        </div>

        {mode === 'adjust' && (
          <div>
            <label htmlFor="stock-reason" className="text-sm font-medium text-ink">
              Reason <span className="text-status-danger">*</span>
            </label>
            <textarea
              id="stock-reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Why is this adjustment being made?"
              className={`${inputClass} h-auto py-2`}
            />
          </div>
        )}

        {error && (
          <div className="flex gap-2 rounded-md bg-status-dangerBg px-3 py-2 text-sm text-status-danger">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <p>{error.message}</p>
          </div>
        )}
      </form>
    </Modal>
  )
}
