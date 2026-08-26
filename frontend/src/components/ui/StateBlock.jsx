import { Loader2, AlertCircle, Inbox } from 'lucide-react'
import Button from './Button.jsx'

/**
 * The three non-data states every data-fetching view must handle (Plan §9.2:
 * "a component with only a happy path is incomplete and will be sent back in review").
 *
 * These are driven by real request state from `useAsync` — nothing here is simulated.
 */

export function LoadingState({ label = 'Loading…' }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-ink-muted">
      <Loader2 className="h-6 w-6 animate-spin" aria-hidden="true" />
      <p className="text-sm" role="status">
        {label}
      </p>
    </div>
  )
}

export function ErrorState({ error, onRetry }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-center">
      <AlertCircle className="h-6 w-6 text-status-danger" aria-hidden="true" />
      <div>
        <p className="text-sm font-semibold text-ink">Something went wrong</p>
        <p className="mt-1 max-w-md text-sm text-ink-muted">
          {error?.message ?? 'The request could not be completed.'}
        </p>
      </div>
      {onRetry && (
        <Button variant="secondary" size="sm" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  )
}

export function EmptyState({ title = 'Nothing to show', description, action }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-center">
      <Inbox className="h-6 w-6 text-ink-subtle" aria-hidden="true" />
      <div>
        <p className="text-sm font-semibold text-ink">{title}</p>
        {description && <p className="mt-1 max-w-md text-sm text-ink-muted">{description}</p>}
      </div>
      {action}
    </div>
  )
}
