const TONES = {
  /** Outlined chip — Inventory "OK". */
  outline: 'border border-surface-border bg-surface-card text-ink-muted',
  /** Filled grey chip — Inventory "WATCH". */
  muted: 'bg-slate-200 text-ink',
  /** Solid dark chip — Inventory "REORDER NOW", Catalogue "MGR APPROVAL REQ". */
  solid: 'bg-status-ok text-white',
  /** Red chip — Catalogue "Low Stock". */
  danger: 'bg-status-dangerBg text-status-danger',
  /** Plain chip with a border — Catalogue "In Stock" / "Out of Stock". */
  plain: 'border border-surface-border bg-surface-card text-ink',
}

const DOTS = {
  info: 'bg-brand-600',
  danger: 'bg-status-danger',
  subtle: 'bg-ink-subtle',
}

/** Shared status chip. `dot` renders the small leading indicator used on catalogue cards. */
export default function Badge({ tone = 'outline', dot, children, className = '' }) {
  return (
    <span
      className={[
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded px-2 py-1 text-xs font-semibold',
        TONES[tone],
        className,
      ].join(' ')}
    >
      {dot && <span className={`h-1.5 w-1.5 rounded-full ${DOTS[dot]}`} aria-hidden="true" />}
      {children}
    </span>
  )
}
