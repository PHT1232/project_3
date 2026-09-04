/**
 * Time-frame picker for the dashboard's "Recent Requests" list. Three modes: this calendar
 * week (Monday start), this calendar month, or a custom From→To range. Filtering happens
 * client-side in the parent — `GET /requests` has no date parameter — over whatever page of
 * recent requests the dashboard already fetched.
 */
const MODES = [
  { id: 'week', label: 'This week' },
  { id: 'month', label: 'This month' },
  { id: 'custom', label: 'Custom' },
]

export const DEFAULT_TIMEFRAME = { mode: 'month', from: '', to: '' }

function toIso(date) {
  return date.toISOString().slice(0, 10)
}

function startOfWeekMonday(now) {
  const day = now.getDay() // 0 = Sun … 6 = Sat
  const backToMonday = day === 0 ? 6 : day - 1
  return new Date(now.getFullYear(), now.getMonth(), now.getDate() - backToMonday)
}

/** Resolve a time-frame value to an inclusive `[fromMs, toMs]` epoch window. */
export function resolveTimeframeWindow(value, now = new Date()) {
  const endOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999).getTime()

  if (value.mode === 'week') {
    return { fromMs: startOfWeekMonday(now).getTime(), toMs: endOfToday }
  }
  if (value.mode === 'month') {
    return { fromMs: new Date(now.getFullYear(), now.getMonth(), 1).getTime(), toMs: endOfToday }
  }
  // custom — missing bounds mean "unbounded on that side"
  const fromMs = value.from ? new Date(`${value.from}T00:00:00`).getTime() : Number.NEGATIVE_INFINITY
  const toMs = value.to ? new Date(`${value.to}T23:59:59.999`).getTime() : endOfToday
  return { fromMs, toMs }
}

const inputClass =
  'h-9 rounded-md border border-surface-border bg-surface-card px-2 text-sm text-ink focus:border-brand-500 focus:outline-none'

export default function RequestTimeframe({ value, onChange }) {
  const maxDate = toIso(new Date())

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
      <div
        className="inline-flex gap-1 rounded-md border border-surface-border bg-surface-card p-1"
        role="group"
        aria-label="Recent requests time frame"
      >
        {MODES.map((mode) => {
          const active = value.mode === mode.id
          return (
            <button
              key={mode.id}
              type="button"
              aria-pressed={active}
              onClick={() => onChange({ ...value, mode: mode.id })}
              className={[
                'h-8 rounded px-3 text-sm font-semibold transition-colors',
                active ? 'bg-brand-700 text-white' : 'text-ink-muted hover:bg-surface-muted hover:text-ink',
              ].join(' ')}
            >
              {mode.label}
            </button>
          )
        })}
      </div>

      {value.mode === 'custom' && (
        <div className="flex items-end gap-2">
          <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-wide text-ink-muted">
            From
            <input
              type="date"
              value={value.from}
              max={value.to || maxDate}
              onChange={(e) => onChange({ ...value, from: e.target.value })}
              className={inputClass}
            />
          </label>
          <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-wide text-ink-muted">
            To
            <input
              type="date"
              value={value.to}
              min={value.from || undefined}
              max={maxDate}
              onChange={(e) => onChange({ ...value, to: e.target.value })}
              className={inputClass}
            />
          </label>
        </div>
      )}
    </div>
  )
}
