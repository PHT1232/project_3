import { useState } from 'react'
import { Mail, Bug, Copy, Check } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import Button from '../../../components/ui/Button.jsx'
import { useAuth } from '../../../contexts/AuthContext.jsx'
import {
  SUPPORT_EMAIL,
  buildDiagnostics,
  buildSupportMailto,
} from '../../../config/support.js'

/**
 * "Contact the team" — a mailto hand-off to the shared support inbox. No mail is sent by the
 * app; the buttons open the user's own mail client with the subject and a body scaffold
 * (plus a diagnostics block) already filled in.
 */
export default function ContactCard() {
  const { user } = useAuth()
  const [copied, setCopied] = useState(false)

  const diagnostics = buildDiagnostics(user)

  const generalMailto = buildSupportMailto({
    subject: 'Stationery system — question',
    diagnostics,
  })
  const bugMailto = buildSupportMailto({
    subject: 'Stationery system — bug report',
    area: 'Something else',
    diagnostics,
  })

  async function copyDiagnostics() {
    try {
      await navigator.clipboard.writeText(diagnostics)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopied(false)
    }
  }

  return (
    <Card className="p-5">
      <h2 className="text-base font-semibold text-ink">Contact the team</h2>
      <p className="mt-1 text-sm text-ink-muted">
        Found a bug or stuck on something? Email us — messages go to{' '}
        <span className="font-medium text-ink">{SUPPORT_EMAIL}</span>.
      </p>

      <div className="mt-4 flex flex-col gap-2">
        <Button as="a" href={bugMailto} variant="primary" className="w-full">
          <Bug className="h-4 w-4" />
          Report a bug
        </Button>
        <Button as="a" href={generalMailto} variant="secondary" className="w-full">
          <Mail className="h-4 w-4" />
          Ask a question
        </Button>
        <Button
          as="button"
          type="button"
          variant="ghost"
          className="w-full"
          onClick={copyDiagnostics}
        >
          {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
          {copied ? 'Copied' : 'Copy diagnostics'}
        </Button>
      </div>

      <p className="mt-3 text-xs text-ink-subtle">
        The email is pre-filled with a template and your session details so we can help faster.
      </p>
    </Card>
  )
}
