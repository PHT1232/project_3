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
