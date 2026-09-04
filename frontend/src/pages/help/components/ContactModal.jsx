import { useEffect, useState } from 'react'
import { CheckCircle2 } from 'lucide-react'

import Modal from '../../../components/ui/Modal.jsx'
import Button from '../../../components/ui/Button.jsx'
import { useAuth } from '../../../contexts/AuthContext.jsx'
import { sendSupportMessage } from '../../../api/support.js'
import { SUPPORT_AREAS, buildDiagnostics } from '../../../config/support.js'

const FIELD =
  'w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink placeholder:text-ink-subtle'

/**
 * The "message the team" dialog. Submits straight into the app's support inbox (no mail
 * client, no email) — a Manager+ reads it at /support-inbox. Session diagnostics are attached
 * automatically; the sender can see exactly what that includes before sending.
 */
export default function ContactModal({ open, onClose }) {
  const { user } = useAuth()
  const [area, setArea] = useState(SUPPORT_AREAS[0])
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [showDiag, setShowDiag] = useState(false)
  const [state, setState] = useState({ status: 'idle', error: null })

  const diagnostics = buildDiagnostics(user)

  useEffect(() => {
    if (!open) {
      setArea(SUPPORT_AREAS[0])
      setSubject('')
      setBody('')
      setShowDiag(false)
      setState({ status: 'idle', error: null })
    }
  }, [open])

  async function submit(e) {
    e.preventDefault()
    setState({ status: 'sending', error: null })
    try {
      await sendSupportMessage({ area, subject: subject.trim(), body: body.trim(), diagnostics })
      setState({ status: 'sent', error: null })
    } catch (err) {
      setState({
        status: 'idle',
        error: err.response?.data?.detail ?? 'Could not send your message. Try again in a moment.',
      })
    }
  }

  const canSend = subject.trim().length > 0 && body.trim().length > 0 && state.status !== 'sending'

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={state.status === 'sent' ? 'Message sent' : 'Message the team'}
      footer={
        state.status === 'sent' ? (
          <Button type="button" onClick={onClose}>
            Done
          </Button>
        ) : (
          <>
            <Button type="button" variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" form="contact-form" disabled={!canSend}>
              {state.status === 'sending' ? 'Sending…' : 'Send'}
            </Button>
          </>
        )
      }
    >
      {state.status === 'sent' ? (
        <div className="flex flex-col items-center gap-3 py-4 text-center">
          <CheckCircle2 className="h-10 w-10 text-status-ok" aria-hidden="true" />
          <p className="text-sm text-ink">
            Thanks — the team can see this now and will follow up. There’s nothing else you need
            to do.
          </p>
        </div>
      ) : (
        <form id="contact-form" onSubmit={submit} className="space-y-4">
          {state.error && (
            <p className="rounded-md bg-status-dangerBg px-3 py-2 text-sm text-status-danger">
              {state.error}
            </p>
          )}

          <label className="block">
            <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-ink-muted">
              Area
            </span>
            <select value={area} onChange={(e) => setArea(e.target.value)} className={FIELD}>
              {SUPPORT_AREAS.map((a) => (
                <option key={a} value={a}>
                  {a}
                </option>
              ))}
            </select>
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-ink-muted">
              Subject
            </span>
            <input
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              maxLength={200}
              placeholder="Short summary"
              className={FIELD}
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-ink-muted">
              Message
            </span>
            <textarea
              value={body}
              onChange={(e) => setBody(e.target.value)}
              maxLength={4000}
              rows={5}
              placeholder="What happened, what you expected, and the steps to reproduce it."
              className={FIELD}
            />
          </label>

          <div className="text-xs text-ink-muted">
            <button
              type="button"
              onClick={() => setShowDiag((v) => !v)}
              className="font-medium text-brand-700 hover:underline"
            >
              {showDiag ? 'Hide' : 'Show'} the session details we’ll attach
            </button>
            {showDiag && (
              <pre className="mt-2 overflow-x-auto whitespace-pre-wrap rounded-md bg-surface-muted p-3 text-[11px] leading-relaxed text-ink-muted">
                {diagnostics}
              </pre>
            )}
          </div>
        </form>
      )}
    </Modal>
  )
}
