import { resolveRangeFromPreset } from '../../../lib/reports.js'

/**
 * Date-range picker for the reports. Presets cover the common windows; the two date
 * inputs map to the `?fromDate=&toDate=` query the real endpoints take (Plan §4.2).
 * Everything is clamped to `bounds` — the span the data actually covers.
 */
const PRESETS = [
  { id: 'D30', label: '30d', days: 30 },
  { id: 'D90', label: '90d', days: 90 },
  { id: 'D365', label: '12mo', days: 365 },
  { id: 'ALL', label: 'All', days: null },
]

function clamp(value, min, max) {
  if (value < min) return min
  if (value > max) return max
  return value
}

export default function DateRangeControl({ value, bounds, onChange }) {
  function applyPreset(days) {
    onChange(resolveRangeFromPreset(days, bounds))
  }

  function setFrom(next) {
    const fromDate = clamp(next, bounds.fromDate, value.toDate)
    onChange({ ...value, fromDate })
  }

  function setTo(next) {
    const toDate = clamp(next, value.fromDate, bounds.toDate)
    onChange({ ...value, toDate })
  }

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
      <div
        className="inline-flex gap-1 rounded-md border border-surface-border bg-surface-card p-1"
        role="group"
        aria-label="Quick date range"
      >
        {PRESETS.map((preset) => (
          <button
            key={preset.id}
            type="button"
            onClick={() => applyPreset(preset.days)}
            className="h-8 rounded px-3 text-sm font-semibold text-ink-muted transition-colors hover:bg-surface-muted hover:text-ink"
          >
            {preset.label}
          </button>
        ))}
      </div>

      <div className="flex items-end gap-2">
        <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-wide text-ink-muted">
          From
          <input
            type="date"
            value={value.fromDate}
            min={bounds.fromDate}
            max={value.toDate}
            onChange={(e) => setFrom(e.target.value)}
            className="h-9 rounded-md border border-surface-border bg-surface-card px-2 text-sm font-normal text-ink focus:border-brand-500 focus:outline-none"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-wide text-ink-muted">
          To
          <input
            type="date"
            value={value.toDate}
            min={value.fromDate}
            max={bounds.toDate}
            onChange={(e) => setTo(e.target.value)}
            className="h-9 rounded-md border border-surface-border bg-surface-card px-2 text-sm font-normal text-ink focus:border-brand-500 focus:outline-none"
          />
        </label>
      </div>
    </div>
  )
}
