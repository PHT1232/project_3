/**
 * Horizontal bar chart for ranking — "most requested items" by units approved.
 * Plain HTML/CSS (no SVG) so labels stay crisp at any width. Single series → a
 * single hue and no legend (dataviz: the title names it); every bar is directly
 * value-labelled. `bars`: [{ label, value }] already sorted; `format(value)` for the label.
 */
export default function BarChart({ bars, format = (v) => v, ariaLabel }) {
  if (!bars || bars.length === 0) {
    return <p className="py-6 text-sm text-ink-muted">Nothing to chart for this selection.</p>
  }

  const maxValue = Math.max(...bars.map((b) => b.value), 1)

  return (
    <ul className="space-y-2.5" aria-label={ariaLabel}>
      {bars.map((bar) => (
        <li key={bar.label} className="grid grid-cols-[minmax(0,11rem)_1fr_auto] items-center gap-3">
          <span className="truncate text-sm text-ink" title={bar.label}>
            {bar.label}
          </span>
          <span className="h-5 overflow-hidden rounded bg-surface-muted" aria-hidden="true">
            <span
              className="block h-full rounded bg-brand-600"
              style={{ width: `${Math.max((bar.value / maxValue) * 100, 1.5)}%` }}
            />
          </span>
          <span className="w-16 text-right text-sm font-semibold tabular-nums text-ink">
            {format(bar.value)}
          </span>
        </li>
      ))}
    </ul>
  )
}
