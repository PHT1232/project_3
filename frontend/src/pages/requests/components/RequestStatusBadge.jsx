import Badge from '../../../components/ui/Badge.jsx'

/**
 * Status chip for Request.Status. Values per Core/Entities/Request.cs and
 * Infrastructure/Services/RequestService.cs — Status is a free-text string, not an enum column.
 */
const STATUS_STYLE = {
  Draft: { tone: 'outline', label: 'Draft' },
  Pending: { tone: 'muted', label: 'Pending' },
  Approved: { tone: 'plain', label: 'Approved' },
  PartiallyApproved: { tone: 'plain', label: 'Partially Approved' },
  Rejected: { tone: 'danger', label: 'Rejected' },
  Withdrawn: { tone: 'outline', label: 'Withdrawn' },
  CancellationPending: { tone: 'muted', label: 'Cancellation Pending' },
  Cancelled: { tone: 'outline', label: 'Cancelled' },
}

export default function RequestStatusBadge({ status }) {
  const style = STATUS_STYLE[status]
  if (!style) return <Badge tone="outline">{status ?? '—'}</Badge>
  return <Badge tone={style.tone}>{style.label}</Badge>
}
