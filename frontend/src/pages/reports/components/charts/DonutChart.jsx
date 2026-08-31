const SIZE = 180
const RADIUS = 80
const INNER = 48
const CENTER = SIZE / 2
const MAX_SLICES = 6

/**
 * Donut (not pie) for part-to-whole — each item's share of approved spend.
 * Ranked data, so a single-hue sequential ramp (darkest = largest), capped at
 * MAX_SLICES + an "Other" roll-up (dataviz: never cycle hues for an Nth series).
 * Identity is carried by the legend text + value, never colour alone.
 */
const RAMP = ['#132555', '#1e3a8a', '#24408f', '#2f4fa8', '#5f79c0', '#9db3e0', '#cbd5e1']

function polar(angle) {
  const rad = ((angle - 90) * Math.PI) / 180
  return { x: CENTER + RADIUS * Math.cos(rad), y: CENTER + RADIUS * Math.sin(rad) }
}

function arcPath(startAngle, endAngle) {
  const start = polar(endAngle)
  const end = polar(startAngle)
  const largeArc = endAngle - startAngle <= 180 ? 0 : 1
  return `M${start.x},${start.y} A${RADIUS},${RADIUS} 0 ${largeArc} 0 ${end.x},${end.y} L${CENTER},${CENTER} Z`
}

export default function DonutChart({ slices, format = (v) => v, ariaLabel }) {
  const positive = (slices ?? []).filter((s) => s.value > 0)
  if (positive.length === 0) {
    return <p className="px-1 py-6 text-sm text-ink-muted">Nothing to chart for this selection.</p>
  }

  const head = positive.slice(0, MAX_SLICES)
  const tail = positive.slice(MAX_SLICES)
  const data = tail.length
    ? [...head, { label: `Other (${tail.length})`, value: tail.reduce((s, x) => s + x.value, 0) }]
    : head

  const total = data.reduce((s, x) => s + x.value, 0)

  let angle = 0
  const wedges = data.map((slice, i) => {
    const sweep = (slice.value / total) * 360
    const wedge = {
      ...slice,
      color: RAMP[Math.min(i, RAMP.length - 1)],
      percent: (slice.value / total) * 100,
      path: sweep >= 359.999 ? null : arcPath(angle, angle + sweep),
      full: sweep >= 359.999,
    }
    angle += sweep
    return wedge
  })

  return (
    <div className="flex flex-col items-center gap-5 sm:flex-row sm:items-center sm:gap-8">
      <svg
        viewBox={`0 0 ${SIZE} ${SIZE}`}
        className="h-44 w-44 shrink-0"
        role="img"
        aria-label={ariaLabel}
      >
        {wedges.map((wedge) =>
          wedge.full ? (
            <circle key={wedge.label} cx={CENTER} cy={CENTER} r={RADIUS} fill={wedge.color}>
              <title>{`${wedge.label}: ${format(wedge.value)} (100%)`}</title>
            </circle>
          ) : (
            <path key={wedge.label} d={wedge.path} fill={wedge.color}>
              <title>{`${wedge.label}: ${format(wedge.value)} (${wedge.percent.toFixed(1)}%)`}</title>
            </path>
          ),
        )}
        <circle cx={CENTER} cy={CENTER} r={INNER} className="fill-surface-card" />
      </svg>

      <ul className="w-full space-y-1.5 text-sm">
        {wedges.map((wedge) => (
          <li key={wedge.label} className="flex items-center gap-2">
            <span
              className="h-3 w-3 shrink-0 rounded-sm"
              style={{ backgroundColor: wedge.color }}
              aria-hidden="true"
            />
            <span className="min-w-0 flex-1 truncate text-ink">{wedge.label}</span>
            <span className="shrink-0 tabular-nums text-ink-muted">{format(wedge.value)}</span>
            <span className="w-14 shrink-0 text-right tabular-nums font-semibold text-ink">
              {wedge.percent.toFixed(1)}%
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
