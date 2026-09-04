import { useEffect, useState } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { formatCurrency, formatDate } from '../../../lib/format.js'
import { approveCancellation } from '../../../api/requests.js'

/**
 * Approver's decision on a CancellationPending request (Plan §3.6):
 *   CancellationPending → Cancelled   (approve the cancellation)
 *   CancellationPending → Approved…   (refuse it — the request stands)
 *
 * Calls POST /approvals/{id}/cancel-approval. Before this modal existed that endpoint had no
 * caller, so a request that entered CancellationPending could never leave it (audit finding C5).
 */
export default function CancellationDecisionModal({ open, request, onClose, onSuccess }) {
  const [reason, setReason] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(null) // 'approve' | 'refuse' | null

  useEffect(() => {
    if (request) {
      setReason('')
      setError(null)
      setSubmitting(null)
    }
  }, [request])

  if (!open || !request) return null

  // The requestor's stated reason is the comment on the transition into CancellationPending.
  const cancellationEntry = [...(request.statusHistory ?? [])]
    .reverse()
    .find((h) => h.toStatus === 'CancellationPending')

  async function decide(approved) {
    setSubmitting(approved ? 'approve' : 'refuse')
    setError(null)
    try {
      await approveCancellation(request.requestId, {
        rowVersion: request.rowVersion,
        approved,
        reason: reason.trim() || null,
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
      setSubmitting(null)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Cancellation request for #${request.requestId}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={Boolean(submitting)}>
            Close
          </Button>
          <Button
            variant="secondary"
            disabled={Boolean(submitting)}
            onClick={() => decide(false)}
            aria-label={`Refuse cancellation of request #${request.requestId}`}
          >
            {submitting === 'refuse' ? 'Refusing…' : 'Refuse cancellation'}
          </Button>
          <Button
            variant="danger"
            disabled={Boolean(submitting)}
            onClick={() => decide(true)}
            aria-label={`Approve cancellation of request #${request.requestId}`}
          >
            {submitting === 'approve' ? 'Cancelling…' : 'Approve cancellation'}
          </Button>
        </>
      }
    >
      <div className="space-y-4 text-sm">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Requestor</p>
            <p className="text-ink">{request.requestorName ?? `#${request.requestorEmployeeNumber}`}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Est. total</p>
            <p className="text-ink">{formatCurrency(request.totalEstimatedCost)}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Requested on</p>
            <p className="text-ink">{cancellationEntry ? formatDate(cancellationEntry.createdAtUtc) : '—'}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Items</p>
            <p className="text-ink">{request.items?.length ?? 0}</p>
          </div>
        </div>

        <div className="rounded-md border border-surface-border bg-surface-muted p-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Requestor's reason</p>
          <p className="mt-1 text-ink">{cancellationEntry?.comment || 'No reason given.'}</p>
        </div>

        <p className="text-ink-muted">
          Approving cancels the request. Refusing leaves it approved as it was.
        </p>

        <div>
          <label htmlFor="cancellation-decision-reason" className="block text-sm font-medium text-ink">
            Your comment (optional)
          </label>
          <textarea
            id="cancellation-decision-reason"
            rows={3}
            maxLength={1000}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Why you are approving or refusing…"
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
          />
        </div>

        {error && (
          <p role="alert" className="text-status-danger">
            {error}
          </p>
        )}
      </div>
    </Modal>
  )
}
