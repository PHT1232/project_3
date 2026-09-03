import { useState } from 'react'
import { Sparkles, AlertCircle, AlertTriangle, Plus, X } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import Button from '../../../components/ui/Button.jsx'
import { draftRequestFromText } from '../../../api/ai.js'
import { formatCurrency } from '../../../lib/format.js'

const MAX_TEXT_LENGTH = 1000

/**
 * A1 — AI Request Assistant (Plan §5.2). Free text in, editable draft out.
 *
 * The draft is shown here for review first; nothing reaches the requisition list until the
 * user clicks "Add to request", and nothing reaches the server until they submit the request
 * through the page's normal Submit button. When the model is unavailable the API answers with
 * a keyword-matched draft and `wasFallback: true`, which is surfaced as an honest notice
 * rather than hidden (Plan §7 M5 acceptance: "displays an honest notice").
 *
 * @param {{ disabled?: boolean, onApplyDraft: (draft) => void }} props
 */
export default function AiAssistantBox({ disabled = false, onApplyDraft }) {
  const [text, setText] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [draft, setDraft] = useState(null)

  const canAsk = !disabled && !loading && text.trim().length > 0

  async function handleDraft(event) {
    event.preventDefault()
    if (!canAsk) return

    setLoading(true)
    setError(null)
    setDraft(null)
    try {
      setDraft(await draftRequestFromText(text.trim()))
    } catch (err) {
      const problem = err.response?.data
      const message =
        err.response?.status === 429
          ? 'You have used the assistant a lot in the last hour. Please pick items from the catalogue below for now.'
          : (problem?.errors ? Object.values(problem.errors).flat().join(', ') : null) ||
            problem?.detail ||
            problem?.title ||
            err.message ||
            'The assistant could not draft a request.'
      setError(message)
    } finally {
      setLoading(false)
    }
  }

  function handleApply() {
    if (!draft || draft.items.length === 0) return
    onApplyDraft(draft)
    setDraft(null)
    setText('')
  }

  return (
    <Card className="p-5">
      <div className="flex items-center gap-2">
        <Sparkles className="h-5 w-5 text-brand-700" aria-hidden="true" />
        <h3 className="text-base font-semibold text-ink">Describe what you need</h3>
      </div>
      <p className="mt-1 text-sm text-ink-muted">
        Type it in plain language — for example “a box of A4 paper and 2 black pens before next
        week” — and the assistant will draft the request for you to review.
      </p>

      <form onSubmit={handleDraft} className="mt-4 space-y-3">
        <label htmlFor="ai-request-text" className="sr-only">
          Describe the stationery you need
        </label>
        <textarea
          id="ai-request-text"
          rows={3}
          maxLength={MAX_TEXT_LENGTH}
          value={text}
          disabled={disabled || loading}
          onChange={(event) => setText(event.target.value)}
          placeholder="I need 3 A4 notebooks and a stapler by Friday…"
          className="w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink focus:border-brand-500 focus:outline-none disabled:opacity-60"
        />
        <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-ink-muted">
            {text.length}/{MAX_TEXT_LENGTH}
          </span>
          <Button type="submit" variant="primary" disabled={!canAsk}>
            <Sparkles className="h-4 w-4" aria-hidden="true" />
            {loading ? 'Drafting…' : 'Draft with AI'}
          </Button>
        </div>
      </form>

      {error && (
        <div
          role="alert"
          className="mt-4 flex items-start gap-3 rounded-lg border border-status-dangerBorder bg-status-dangerBg p-3 text-sm text-status-danger"
        >
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="font-medium">{error}</span>
        </div>
      )}

      {draft && (
        <div className="mt-4 space-y-3" data-testid="ai-draft">
          {draft.wasFallback && (
            <div className="flex items-start gap-3 rounded-lg border border-surface-border bg-surface-muted p-3 text-sm text-ink">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-ink-muted" aria-hidden="true" />
              <span>
                <span className="font-medium">AI assistant unavailable.</span> Items were matched by
                keyword instead — please double-check them.
              </span>
            </div>
          )}

          {draft.warnings
            .filter((warning) => !draft.wasFallback || !warning.startsWith('The AI assistant was unavailable'))
            .map((warning) => (
              <p key={warning} className="flex items-start gap-2 text-sm text-ink-muted">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                {warning}
              </p>
            ))}

          {draft.items.length > 0 && (
            <div className="overflow-x-auto rounded-md border border-surface-border">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-surface-border bg-surface-muted text-xs uppercase tracking-wider text-ink-muted">
                    <th className="px-3 py-2 font-semibold">Item</th>
                    <th className="px-3 py-2 text-center font-semibold">Qty</th>
                    <th className="px-3 py-2 text-right font-semibold">Est. Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-surface-border">
                  {draft.items.map((item) => (
                    <tr key={item.itemId}>
                      <td className="px-3 py-2">
                        <p className="font-medium text-ink">{item.itemName}</p>
                        <p className="text-xs text-ink-muted">
                          {item.categoryName ?? 'General'} · {item.quantityAvailable} available
                        </p>
                      </td>
                      <td className="px-3 py-2 text-center font-mono text-ink">{item.quantity}</td>
                      <td className="px-3 py-2 text-right font-mono text-ink">
                        {formatCurrency(item.quantity * item.unitCost)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {draft.note && <p className="text-sm italic text-ink-muted">{draft.note}</p>}

          <div className="flex flex-wrap items-center justify-between gap-3">
            <span className="text-sm text-ink-muted">
              {draft.requiredByDate
                ? `Required by ${new Date(draft.requiredByDate).toLocaleDateString()}`
                : 'No required-by date suggested'}
              {' · '}
              <span className="font-mono font-medium text-ink">{formatCurrency(draft.totalEstimatedCost)}</span>
            </span>
            <div className="flex gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setDraft(null)}>
                <X className="h-4 w-4" aria-hidden="true" />
                Discard
              </Button>
              <Button
                type="button"
                variant="primary"
                size="sm"
                disabled={draft.items.length === 0}
                onClick={handleApply}
              >
                <Plus className="h-4 w-4" aria-hidden="true" />
                Add to request
              </Button>
            </div>
          </div>
        </div>
      )}
    </Card>
  )
}
