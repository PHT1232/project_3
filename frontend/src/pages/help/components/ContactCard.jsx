import { useState } from 'react'
import { MessageSquarePlus, Copy, Check } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import Button from '../../../components/ui/Button.jsx'
import { useAuth } from '../../../contexts/AuthContext.jsx'
import { SUPPORT_EMAIL, buildDiagnostics } from '../../../config/support.js'
import ContactModal from './ContactModal.jsx'

/**
 * "Contact the team". The primary path is an in-app message that lands in the support inbox
 * (a Manager+ triages it) — no mail client, works offline. The email address is kept only as
 * a manual fallback.
 */
export default function ContactCard() {
  const { user } = useAuth()
  const [modalOpen, setModalOpen] = useState(false)
  const [copied, setCopied] = useState(false)

  async function copyDiagnostics() {
    try {
      await navigator.clipboard.writeText(buildDiagnostics(user))
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
        Found a bug or stuck on something? Send us a message — it goes straight to the team’s
        inbox and they’ll follow up.
      </p>

      <div className="mt-4 flex flex-col gap-2">
        <Button type="button" onClick={() => setModalOpen(true)} className="w-full">
          <MessageSquarePlus className="h-4 w-4" />
          Message the team
        </Button>
        <Button type="button" variant="ghost" className="w-full" onClick={copyDiagnostics}>
          {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
          {copied ? 'Copied' : 'Copy diagnostics'}
        </Button>
      </div>

      <p className="mt-3 text-xs text-ink-subtle">
        Prefer email? Write to{' '}
        <a href={`mailto:${SUPPORT_EMAIL}`} className="font-medium text-ink hover:underline">
          {SUPPORT_EMAIL}
        </a>
        .
      </p>

      <ContactModal open={modalOpen} onClose={() => setModalOpen(false)} />
    </Card>
  )
}
