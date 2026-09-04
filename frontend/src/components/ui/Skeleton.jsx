/**
 * Shared skeleton-loading primitives.
 *
 * These replace the centred spinner (`LoadingState`) on views whose shape is known in advance,
 * so the page reserves the same space it will occupy once the data arrives — no layout jump
 * between the loading and loaded states (Plan §9.2 requires loading/error/empty on every
 * data-fetching view; this changes how *loading* looks, not the rule).
 *
 * `LoadingState` is deliberately kept for the cases a skeleton cannot describe: a whole-app
 * gate (session restore) and short in-modal waits.
 *
 * Two rules keep a placeholder aligned with the content it stands in for:
 *   - bar heights match the *line box* of the text they replace (text-xs → h-4, text-sm → h-5,
 *     text-lg → h-7, text-3xl → h-9), so a placeholder row is as tall as the real one;
 *   - cell padding is passed in, because the tables here are not all `px-4` — the dashboard
 *     panel is `px-5` and the request picker `px-3`.
 *
 * Accessibility: every composite exposes a visually-hidden `role="status"` label with the same
 * wording the spinner used, so screen readers (and the existing tests) still hear "Loading …".
 * The blocks themselves are `aria-hidden` decoration.
 *
 * SHARED FILE — add a pattern here only when two or more pages need it; one-off page shapes
 * belong next to the page.
 */

/** One shimmering block. `className` sets its size (and any shape override). */
export function Skeleton({ className = '' }) {
  return <div aria-hidden="true" className={`animate-pulse rounded bg-surface-border ${className}`} />
}

/** Wraps a skeleton in the status/label plumbing so each composite doesn't repeat it. */
function SkeletonFrame({ label, className = '', children }) {
  return (
    <div role="status" aria-busy="true" className={className}>
      <span className="sr-only">{label}</span>
      {children}
    </div>
  )
}

/** Stacked text lines; the last one is short, the way a wrapped paragraph ends. */
export function SkeletonText({ lines = 3, className = '' }) {
  return (
    <div className={`space-y-2 ${className}`}>
      {Array.from({ length: lines }, (_, index) => (
        <Skeleton key={index} className={`h-4 ${index === lines - 1 ? 'w-2/3' : 'w-full'}`} />
      ))}
    </div>
  )
}

/**
 * Table placeholder.
 *
 * `columns` describes the table being stood in for — a plain count, or one entry per column so
 * the placeholder lands where the real data will:
 *   number             — the column's share of the table width, relative to the other columns
 *   { width }          — the same share, written out
 *   { align: 'right' } — right-aligned, for the numeric and action columns
 *   { bar }            — a narrower bar for a narrow column (a checkbox, an icon button)
 *   { height }         — a taller bar where the real cell holds something taller than a line of
 *                        text: `h-6` for a status badge, `h-8` for a row of small buttons. The
 *                        tallest bar sets the row height, exactly as the real content does, so
 *                        an action-column row is 57px in both.
 *
 * The table is `table-fixed` with a `<colgroup>` so those shares are honoured exactly. Without
 * it the browser sizes the cells by their content, and since skeleton cells hold fixed-width
 * blocks rather than text, every column collapses towards the left instead of spanning the
 * table the way the loaded one does.
 */
export function SkeletonTable({
  columns = 5,
  rows = 6,
  label = 'Loading…',
  cellClassName = 'px-4 py-3',
  className = '',
}) {
  const entries = (Array.isArray(columns) ? columns : Array.from({ length: columns }, () => 1)).map(
    (entry) => (typeof entry === 'number' ? { width: entry } : entry),
  )
  const total = entries.reduce((sum, entry) => sum + (entry.width ?? 1), 0)

  function Cell({ entry, height, barWidth }) {
    return (
      <td className={cellClassName}>
        <div className={`flex ${entry.align === 'right' ? 'justify-end' : ''}`}>
          <Skeleton className={`${height} ${entry.bar ?? barWidth}`} />
        </div>
      </td>
    )
  }

  return (
    <SkeletonFrame label={label} className={`overflow-x-auto ${className}`}>
      <table className="w-full table-fixed text-left text-sm">
        <colgroup>
          {entries.map((entry, index) => (
            <col key={index} style={{ width: `${((entry.width ?? 1) / total) * 100}%` }} />
          ))}
        </colgroup>
        <tbody>
          {/* Header row — `text-xs` up there, so a shorter bar than the body rows below. */}
          <tr className="border-b border-surface-border">
            {entries.map((entry, index) => (
              <Cell key={index} entry={entry} height="h-4" barWidth="w-2/3" />
            ))}
          </tr>
          {Array.from({ length: rows }, (_, rowIndex) => (
            <tr key={rowIndex} className="border-b border-surface-border last:border-0">
              {entries.map((entry, index) => (
                <Cell key={index} entry={entry} height={entry.height ?? 'h-5'} barWidth="w-5/6" />
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </SkeletonFrame>
  )
}

/**
 * The KPI / summary tile row. `kpi` matches the richer Dashboard tile (icon square on the
 * right plus a supporting line); the default matches the plain `StatCard` used by Inventory
 * and Reports.
 */
export function SkeletonStatCards({
  count = 3,
  kpi = false,
  label = 'Loading…',
  grid = 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
  className = '',
}) {
  return (
    <SkeletonFrame label={label} className={`grid gap-5 ${grid} ${className}`}>
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          className={`rounded-card border border-surface-border bg-surface-card ${kpi ? 'p-5' : 'px-5 py-4'}`}
        >
          {/* The caption line, beside the KPI icon tile that sets this row's height. */}
          <div className="flex items-start justify-between gap-4">
            <Skeleton className="h-4 w-28" />
            {kpi && <Skeleton className="h-10 w-10 shrink-0 rounded-lg" />}
          </div>
          {/* The figure itself — text-3xl. */}
          <Skeleton className="mt-2 h-9 w-24" />
          {kpi && <Skeleton className="mt-2 h-5 w-40 max-w-full" />}
        </div>
      ))}
    </SkeletonFrame>
  )
}

/** Catalogue product-card grid: image band, title, meta line, then the cost and action row. */
export function SkeletonCardGrid({ count = 6, label = 'Loading…' }) {
  return (
    <SkeletonFrame label={label} className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
      {Array.from({ length: count }, (_, index) => (
        <div
          key={index}
          className="flex flex-col overflow-hidden rounded-card border border-surface-border bg-surface-card"
        >
          <div className="h-40 bg-surface-muted" aria-hidden="true" />
          <div className="flex flex-1 flex-col p-4">
            <Skeleton className="h-[1.375rem] w-3/4" />
            <Skeleton className="mt-1 h-4 w-1/2" />
            <hr className="my-4 border-surface-border" />
            {/* "Est. Cost" caption over the figure, with the Add Request button (h-10) opposite. */}
            <div className="mt-auto flex items-end justify-between gap-3">
              <div className="min-w-0">
                <Skeleton className="h-4 w-16" />
                <Skeleton className="h-7 w-20" />
              </div>
              <Skeleton className="h-10 w-32 rounded-md" />
            </div>
          </div>
        </div>
      ))}
    </SkeletonFrame>
  )
}

/**
 * Stacked rows for list-shaped content (a modal list, the notification feed): a title line,
 * one or two supporting lines, and an optional trailing control.
 *
 * `rowClassName` carries the row's own padding, which differs per list.
 */
export function SkeletonList({
  rows = 4,
  lines = 2,
  trailing = true,
  rowClassName = 'py-3',
  label = 'Loading…',
  className = '',
}) {
  const supportingWidths = ['w-1/2', 'w-1/4']

  return (
    <SkeletonFrame label={label} className={`divide-y divide-surface-border ${className}`}>
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} className={`flex items-center justify-between gap-3 ${rowClassName}`}>
          <div className="min-w-0 flex-1">
            <Skeleton className="h-5 w-1/3" />
            {Array.from({ length: lines - 1 }, (_, line) => (
              <Skeleton key={line} className={`mt-1 h-4 ${supportingWidths[line] ?? 'w-1/3'}`} />
            ))}
          </div>
          {trailing && <Skeleton className="h-8 w-20 shrink-0 rounded-md" />}
        </div>
      ))}
    </SkeletonFrame>
  )
}
