/**
 * Document-style metadata strip shown below the tab row on every Reports tab.
 * Reads like a report header, not a KPI card: small muted text in a tinted band.
 *
 * Props:
 *   generatedAt : Date   — when the currently displayed data was loaded
 *   fromDate/toDate : 'YYYY-MM-DD' — the report period (ignored when `snapshot`)
 *   snapshot : boolean   — true for point-in-time tabs (Inventory Valuation);
 *                          shows "As of <today>" instead of a period
 */

function monthDayYear(iso) {
  return new Date(`${iso}T00:00:00Z`).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    timeZone: 'UTC',
  })
}

function generatedLabel(date) {
  const day = date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
  const time = date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })
  return `${day} at ${time}`
}

export default function ReportMetaBar({ generatedAt, fromDate, toDate, snapshot = false }) {
  const parts = [
    `Generated: ${generatedLabel(generatedAt)}`,
    snapshot
      ? `As of ${monthDayYear(toDate)}`
      : `Period: ${monthDayYear(fromDate)} – ${monthDayYear(toDate)}`,
    'Approved requests only',
    'Manager view',
  ]

  return (
    <div
      data-print-region
      className="mb-5 rounded-card border border-surface-border bg-surface-muted px-4 py-2 text-xs text-ink-muted"
    >
      {parts.map((part, i) => (
        <span key={part}>
          {i > 0 && <span className="mx-2 text-ink-subtle" aria-hidden="true">·</span>}
          {part}
        </span>
      ))}
    </div>
  )
}
