import { useState } from 'react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import Badge from '../../components/ui/Badge.jsx'
import { ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import { Skeleton, SkeletonText } from '../../components/ui/Skeleton.jsx'
import useAsync from '../../hooks/useAsync.js'
import { useAuth } from '../../contexts/AuthContext.jsx'
import { formatDate } from '../../lib/format.js'
import { getSupportMessages, setSupportMessageResolved } from '../../api/support.js'

const FILTERS = [
  { key: 'New', label: 'Open' },
  { key: 'Resolved', label: 'Resolved' },
  { key: '', label: 'All' },
]

/**
 * Manager+ triage screen for messages sent from the Help page. Route-guarded as Manager+
 * client-side (nav hiding is UX only — the server 403 on `/api/v1/support/messages` is the
 * real control).
 */
export default function SupportInboxPage() {
  const { user } = useAuth()
  const [filter, setFilter] = useState('New')
  const [busyId, setBusyId] = useState(null)

  const { data, error, loading, reload } = useAsync(
    () => getSupportMessages({ status: filter, pageSize: 100 }),
    [filter],
  )

  const messages = data?.items ?? []

  async function toggle(message) {
    setBusyId(message.id)
    try {
      await setSupportMessageResolved(message.id, message.status !== 'Resolved')
      await reload()
    } finally {
      setBusyId(null)
    }
  }

  return (
    <>
      <PageHeader
        title="Support inbox"
        description="Bug reports and questions sent from the Help page."
      />

      <div className="mb-4 flex gap-2">
        {FILTERS.map((f) => (
          <Button
            key={f.label}
            size="sm"
            variant={filter === f.key ? 'primary' : 'secondary'}
            onClick={() => setFilter(f.key)}
          >
            {f.label}
          </Button>
        ))}
      </div>

      {loading && <SupportInboxSkeleton />}
      {!loading && error && <ErrorState error={error} onRetry={reload} />}
      {!loading && !error && messages.length === 0 && (
        <Card className="p-0">
          <EmptyState
            title={filter === 'New' ? 'Nothing open' : 'No messages'}
            description={
              filter === 'New'
                ? 'Messages from the Help page will appear here.'
                : 'Nothing matches this filter.'
            }
          />
        </Card>
      )}

      {!loading && !error && messages.length > 0 && (
        <ul className="space-y-3">
          {messages.map((m) => (
            <li key={m.id}>
              <MessageCard
                message={m}
                busy={busyId === m.id}
                sentByViewer={m.senderEmployeeNumber === user?.employeeNumber}
                onToggle={() => toggle(m)}
              />
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

/**
 * Loading placeholder for the message list — same card footprint as {@link MessageCard}
 * (header line + trailing button, then a two-line body) so the page doesn't jump when the
 * data arrives. Matches the skeleton treatment on the other list pages.
 */
function SupportInboxSkeleton({ rows = 4 }) {
  return (
    <div role="status" aria-busy="true" className="space-y-3">
      <span className="sr-only">Loading messages…</span>
      {Array.from({ length: rows }, (_, i) => (
        <Card key={i} className="p-4" aria-hidden="true">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0 flex-1">
              <Skeleton className="h-5 w-2/5" />
              <Skeleton className="mt-1.5 h-4 w-1/4" />
            </div>
            <Skeleton className="h-8 w-28 shrink-0 rounded-md" />
          </div>
          <SkeletonText lines={2} className="mt-3" />
        </Card>
      ))}
    </div>
  )
}

function MessageCard({ message, busy, sentByViewer, onToggle }) {
  const [showDiag, setShowDiag] = useState(false)
  const resolved = message.status === 'Resolved'

  return (
    <Card className="p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-sm font-semibold text-ink">{message.subject}</h2>
            <Badge tone="outline">{message.area}</Badge>
            {resolved && <Badge tone="muted">Resolved</Badge>}
          </div>
          <p className="mt-0.5 text-xs text-ink-muted">
            {message.senderName || `#${message.senderEmployeeNumber}`} · {formatDate(message.createdAtUtc)}
            {resolved && message.resolvedByName ? ` · resolved by ${message.resolvedByName}` : ''}
          </p>
        </div>
        {sentByViewer ? (
          <Badge tone="outline">You sent this</Badge>
        ) : (
          <Button
            size="sm"
            variant={resolved ? 'secondary' : 'primary'}
            disabled={busy}
            onClick={onToggle}
          >
            {busy ? '…' : resolved ? 'Reopen' : 'Mark resolved'}
          </Button>
        )}
      </div>

      <p className="mt-3 whitespace-pre-wrap text-sm leading-relaxed text-ink">{message.body}</p>

      {message.diagnostics && (
        <div className="mt-3 text-xs">
          <button
            type="button"
            onClick={() => setShowDiag((v) => !v)}
            className="font-medium text-brand-700 hover:underline"
          >
            {showDiag ? 'Hide' : 'Show'} session details
          </button>
          {showDiag && (
            <pre className="mt-2 overflow-x-auto whitespace-pre-wrap rounded-md bg-surface-muted p-3 text-[11px] leading-relaxed text-ink-muted">
              {message.diagnostics}
            </pre>
          )}
        </div>
      )}
    </Card>
  )
}
