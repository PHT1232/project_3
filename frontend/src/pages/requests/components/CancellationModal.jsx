import { useState, useEffect } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'

export default function CancellationModal({
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
      title={`Request Cancellation for #${request.requestId}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button
            variant="danger"
            type="submit"
            form="cancel-request-form"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Requesting…' : 'Submit Cancellation Request'}
          </Button>
        </>
      }
    >
      <form id="cancel-request-form" onSubmit={handleSubmit} className="space-y-4">
        <p className="text-sm text-ink-muted">
          Are you sure you want to request cancellation for request #{request.requestId}?
          This will notify your approver for final confirmation.
        </p>

        <div>
          <label htmlFor="cancel-reason" className="block text-sm font-medium text-ink">
            Reason for cancellation (optional, max 500 characters)
          </label>
          <textarea
            id="cancel-reason"
            rows={3}
            maxLength={500}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Why is this request being cancelled?"
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
