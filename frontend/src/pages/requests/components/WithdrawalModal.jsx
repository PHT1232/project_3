import { useState, useEffect } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'

/**
 * Requestor asks to withdraw a Pending request: Pending → WithdrawalPending.
 *
 * Deliberately the same shape as CancellationModal, because since 2026-09-07 the two
 * requestor-initiated revocations work the same way — neither takes effect until the approver
 * confirms it. This replaced a bare window.confirm(), which matched the old behaviour where
 * withdrawing was immediate and final.
 */
export default function WithdrawalModal({
  open,
  request,
  onClose,
  onConfirm,
  isSubmitting = false,
}) {
  const [reason, setReason] = useState('')

  useEffect(() => {
    if (open) {
      setReason('')
    }
  }, [open])

  if (!open || !request) return null

  function handleSubmit(e) {
    e.preventDefault()
    onConfirm(request, reason.trim() || null)
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Request Withdrawal for #${request.requestId}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            variant="danger"
            type="submit"
            form="withdraw-request-form"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Requesting…' : 'Submit Withdrawal Request'}
          </Button>
        </>
      }
    >
      <form id="withdraw-request-form" onSubmit={handleSubmit} className="space-y-4">
        <p className="text-sm text-ink-muted">
          Are you sure you want to request withdrawal of request #{request.requestId}? Your
          approver has to confirm it — the request stays with them until they do, and they can
          refuse and decide on it as normal.
        </p>

        <div>
          <label htmlFor="withdraw-reason" className="block text-sm font-medium text-ink">
            Reason for withdrawal (optional, max 500 characters)
          </label>
          <textarea
            id="withdraw-reason"
            rows={3}
            maxLength={500}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Why is this request being withdrawn?"
            className="mt-1 w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink placeholder:text-ink-subtle focus:border-brand-500 focus:outline-none"
          />
          <div className="mt-1 text-right text-xs text-ink-muted">
            {reason.length}/500
          </div>
        </div>
      </form>
    </Modal>
  )
}
