const W = 640
const H = 200
const PAD = { top: 14, right: 14, bottom: 24, left: 14 }

/**
 * Single-series line chart with a faint area fill — for change over time
 * (monthly spend, cumulative spend). One measure, one axis (dataviz: never dual-axis).
 * `points`: [{ label, value }] in chronological order. `format(value)` labels the tooltip.
 */
export default function LineChart({ points, format = (v) => v, ariaLabel }) {
  if (!points || points.length < 2) {
    return (
      <p className="px-1 py-6 text-sm text-ink-muted">
        Not enough data in this range to plot a trend.
      </p>
    )
  }

  const innerW = W - PAD.left - PAD.right
  const innerH = H - PAD.top - PAD.bottom
  const maxValue = Math.max(...points.map((p) => p.value), 1)

  const coords = points.map((point, i) => ({
    ...point,
    x: PAD.left + (i / (points.length - 1)) * innerW,
    y: PAD.top + innerH - (point.value / maxValue) * innerH,
  }))

  const line = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x},${c.y}`).join(' ')
  const area =
    `M${coords[0].x},${PAD.top + innerH} ` +
    coords.map((c) => `L${c.x},${c.y}`).join(' ') +
    ` L${coords.at(-1).x},${PAD.top + innerH} Z`

  return (
    <svg
      viewBox={`0 0 ${W} ${H}`}
      className="h-48 w-full text-brand-600"
      role="img"
      aria-label={ariaLabel}
      preserveAspectRatio="none"
    >
      <line
        x1={PAD.left}
        y1={PAD.top + innerH}
        x2={W - PAD.right}
        y2={PAD.top + innerH}
        className="text-surface-border"
        stroke="currentColor"
        strokeWidth="1"
        vectorEffect="non-scaling-stroke"
      />
      <path d={area} fill="currentColor" fillOpacity="0.12" />
      <path
        d={line}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        vectorEffect="non-scaling-stroke"
      />
      {coords.map((c) => (
        <circle key={c.label} cx={c.x} cy={c.y} r="2.5" fill="currentColor">
          <title>{`${c.label}: ${format(c.value)}`}</title>
        </circle>
      ))}
      {coords.map((c, i) => (
        <text
          key={`lbl-${c.label}`}
          x={c.x}
          y={H - 6}
          textAnchor={i === 0 ? 'start' : i === coords.length - 1 ? 'end' : 'middle'}
          className="fill-ink-subtle text-[9px]"
        >
          {c.label}
        </text>
      ))}
    </svg>
  )
}
