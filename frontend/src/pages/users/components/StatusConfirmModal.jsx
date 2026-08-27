import { useState } from 'react'
import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'

export default function StatusConfirmModal({ open, onClose, onConfirm, user }) {
  const [submitting, setSubmitting] = useState(false)
  if (!user) return null

  const nextActive = !user.isActive
  const verb = nextActive ? 'Activate' : 'Deactivate'

  async function handleConfirm() {
    setSubmitting(true)
    try {
      await onConfirm(nextActive)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`${verb} ${user.name}?`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleConfirm} disabled={submitting}>
            {submitting ? 'Saving…' : verb}
          </Button>
        </>
      }
    >
      <p className="text-sm text-ink-muted">
        {nextActive
          ? `#${user.employeeNumber} will be able to sign in again.`
          : `#${user.employeeNumber} will be signed out immediately and can no longer sign in.`}
      </p>
    </Modal>
  )
}
