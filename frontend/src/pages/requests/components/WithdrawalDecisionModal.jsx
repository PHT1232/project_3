import { useEffect, useState } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { formatCurrency, formatDate } from '../../../lib/format.js'
import { approveWithdrawal } from '../../../api/requests.js'

/**
 * Approver's decision on a WithdrawalPending request:
 *   WithdrawalPending → Withdrawn   (confirm the withdrawal — final)
 *   WithdrawalPending → Pending     (refuse it — the request returns to this queue)
 *
 * Calls POST /approvals/{id}/withdrawal-approval. Mirrors CancellationDecisionModal, because
 * since 2026-09-07 a requestor cannot revoke a request on their own by either route.
 */
export default function WithdrawalDecisionModal({ open, request, onClose, onSuccess }) {
  const [reason, setReason] = useState('')
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(null) // 'confirm' | 'refuse' | null

  useEffect(() => {
    if (request) {
      setReason('')
      setError(null)
      setSubmitting(null)
    }
  }, [request])

  if (!open || !request) return null

  // The requestor's stated reason is the comment on the transition into WithdrawalPending.
  const withdrawalEntry = [...(request.statusHistory ?? [])]
    .reverse()
    .find((h) => h.toStatus === 'WithdrawalPending')

  async function decide(approved) {
    setSubmitting(approved ? 'confirm' : 'refuse')
    setError(null)
    try {
      await approveWithdrawal(request.requestId, {
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
      title={`Withdrawal request for #${request.requestId}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={Boolean(submitting)}>
            Close
          </Button>
          <Button
            variant="secondary"
            disabled={Boolean(submitting)}
            onClick={() => decide(false)}
            aria-label={`Refuse withdrawal of request #${request.requestId}`}
          >
            {submitting === 'refuse' ? 'Refusing…' : 'Refuse withdrawal'}
          </Button>
          <Button
            variant="danger"
            disabled={Boolean(submitting)}
            onClick={() => decide(true)}
            aria-label={`Confirm withdrawal of request #${request.requestId}`}
          >
            {submitting === 'confirm' ? 'Withdrawing…' : 'Confirm withdrawal'}
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
            <p className="text-ink">{withdrawalEntry ? formatDate(withdrawalEntry.createdAtUtc) : '—'}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Items</p>
            <p className="text-ink">{request.items?.length ?? 0}</p>
          </div>
        </div>

        <div className="rounded-md border border-surface-border bg-surface-muted p-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Requestor's reason</p>
          <p className="mt-1 text-ink">{withdrawalEntry?.comment || 'No reason given.'}</p>
        </div>

        <p className="text-ink-muted">
          Confirming withdraws the request for good. Refusing puts it back in your queue to
          approve or reject as normal. No stock moves either way — nothing was issued yet.
        </p>

        <div>
          <label htmlFor="withdrawal-decision-reason" className="block text-sm font-medium text-ink">
            Your comment (optional)
          </label>
          <textarea
            id="withdrawal-decision-reason"
            rows={3}
            maxLength={500}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Why you are confirming or refusing…"
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
