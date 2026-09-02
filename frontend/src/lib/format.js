/**
 * Display formatting helpers.
 *
 * CURRENCY IS UNRESOLVED. The approved wireframes show `$`, but the Plan's open question
 * `[ASK] #10` records the working default as **VND**, `decimal(18,2)`. The wireframes are the
 * approved UI reference, so `$` is used here — but this is the single place to change it once
 * the instructor answers. Do not inline currency symbols anywhere else.
 */
const CURRENCY = { locale: 'en-US', code: 'USD' }

export function formatCurrency(amount) {
  if (amount == null || Number.isNaN(amount)) return '—'
  return new Intl.NumberFormat(CURRENCY.locale, {
    style: 'currency',
    currency: CURRENCY.code,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount)
}

export function formatNumber(value) {
  if (value == null || Number.isNaN(value)) return '—'
  return new Intl.NumberFormat('en-US').format(value)
}

/** Dates are stored/transmitted as UTC ISO strings (CLAUDE.md principle #11); render local. */
export function formatDate(value) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' }).format(new Date(value))
}

/** Formats a timestamp into a friendly relative description like "5m ago", "2h ago", "Yesterday". */
export function formatRelativeTime(value) {
  if (!value) return '—'
  const date = new Date(value)
  const now = new Date()
  const diffMs = now - date
  const diffSeconds = Math.max(0, Math.floor(diffMs / 1000))
  const diffMinutes = Math.floor(diffSeconds / 60)
  const diffHours = Math.floor(diffMinutes / 60)
  const diffDays = Math.floor(diffHours / 24)

  if (diffSeconds < 60) return 'Just now'
  if (diffMinutes < 60) return `${diffMinutes}m ago`
  if (diffHours < 24) return `${diffHours}h ago`
  if (diffDays === 1) return 'Yesterday'
  if (diffDays < 7) return `${diffDays}d ago`
  return formatDate(value)
}

