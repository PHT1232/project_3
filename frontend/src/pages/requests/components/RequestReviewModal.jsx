import { useEffect, useState } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { formatCurrency } from '../../../lib/format.js'
import { approveRequest } from '../../../api/requests.js'

const DECISIONS = [
  { value: 'approved', label: 'Approve' },
  { value: 'rejected', label: 'Reject' },
  { value: 'modified', label: 'Modify qty' },
]

function initialDecisions(request) {
  const state = {}
  for (const item of request.items) {
    state[item.requestItemId] = { decision: 'approved', modifiedQuantity: String(item.quantity) }
  }
  return state
}

/**
 * Per-line approve/reject/modify review, matching ApproveRequestCommand's shape
 * (Application/DTOs/Requests/ApproveRequestCommand.cs). One decision per line is required —
 * the backend rejects the whole request if LineDecisions.Count doesn't match Items.Count.
 */
export default function RequestReviewModal({ open, request, onClose, onSuccess }) {
  const [decisions, setDecisions] = useState({})
  const [comment, setComment] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)

  // The modal stays mounted (open toggles), so a plain useState initializer only ever runs
  // once — reset the form explicitly whenever a different request is opened for review.
  useEffect(() => {
    if (request) {
      setDecisions(initialDecisions(request))
      setComment('')
      setError(null)
      setSubmitting(false)
    }
  }, [request])

  if (!open || !request) return null

  function updateDecision(requestItemId, patch) {
    setDecisions((current) => ({
      ...current,
      [requestItemId]: { ...current[requestItemId], ...patch },
    }))
  }

  const canSubmit = Object.values(decisions).every(
    (d) => d.decision !== 'modified' || (Number(d.modifiedQuantity) > 0 && Number.isFinite(Number(d.modifiedQuantity))),
  )

  async function handleSubmit(event) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      const lineDecisions = request.items.map((item) => {
        const d = decisions[item.requestItemId]
        return {
          requestItemId: item.requestItemId,
          decision: d.decision,
          modifiedQuantity: d.decision === 'modified' ? Number(d.modifiedQuantity) : null,
        }
      })

      await approveRequest(request.requestId, {
        rowVersion: request.rowVersion,
        lineDecisions,
        comment: comment.trim() || null,
      })
      onSuccess?.()
      onClose()
    } catch (err) {
      setError(
        err.response?.data?.detail ??
          err.response?.data?.error ??
          err.message ??
          'Something went wrong.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Review request #${request.requestId}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button form="request-review-form" type="submit" disabled={!canSubmit || submitting}>
            {submitting ? 'Submitting…' : 'Submit decision'}
          </Button>
        </>
      }
    >
      <form id="request-review-form" onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-3 text-sm">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Requestor</p>
            <p className="text-ink">{request.requestorName ?? `#${request.requestorEmployeeNumber}`}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Est. total</p>
            <p className="text-ink">{formatCurrency(request.totalEstimatedCost)}</p>
          </div>
        </div>

        <div className="overflow-x-auto rounded-md border border-surface-border">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-surface-border bg-surface-muted text-xs uppercase tracking-wide text-ink-muted">
                <th className="px-3 py-2">Item</th>
                <th className="px-3 py-2 text-right">Qty</th>
                <th className="px-3 py-2">Decision</th>
                <th className="px-3 py-2 text-right">Modified qty</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-surface-border">
              {request.items.map((item) => {
                // Falls back to a sensible default for the render that introduces a newly
                // selected request — the reset effect updates `decisions` from that render's
                // props, but effects commit after render, so this render still needs a value.
                const d = decisions[item.requestItemId] ?? {
                  decision: 'approved',
                  modifiedQuantity: String(item.quantity),
                }
                return (
                  <tr key={item.requestItemId}>
                    <td className="px-3 py-2 text-ink">{item.itemName}</td>
                    <td className="px-3 py-2 text-right text-ink-muted">{item.quantity}</td>
                    <td className="px-3 py-2">
                      <select
                        value={d.decision}
                        onChange={(e) => updateDecision(item.requestItemId, { decision: e.target.value })}
                        className="h-9 rounded-md border border-surface-border bg-surface-card px-2 text-sm text-ink"
                      >
                        {DECISIONS.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td className="px-3 py-2 text-right">
                      <input
                        type="number"
                        min={1}
                        disabled={d.decision !== 'modified'}
                        value={d.modifiedQuantity}
                        onChange={(e) => updateDecision(item.requestItemId, { modifiedQuantity: e.target.value })}
                        className="h-9 w-20 rounded-md border border-surface-border bg-surface-card px-2 text-right text-sm text-ink disabled:cursor-not-allowed disabled:bg-surface-muted disabled:text-ink-subtle"
                      />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>

        <div>
          <label htmlFor="review-comment" className="block text-sm font-medium text-ink">
            Comment (optional)
          </label>
          <textarea
            id="review-comment"
            rows={3}
            maxLength={1000}
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder="Reason for this decision…"
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>

        {error && (
          <p role="alert" className="text-sm text-status-danger">
            {error}
          </p>
        )}
      </form>
    </Modal>
  )
}
