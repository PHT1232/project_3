/**
 * Segmented control that switches between the three cost reports. The reports are
 * "three distinct views, not one page with three columns" (page-map §9), so this is
 * a real tablist — each view fetches its own endpoint.
 */
export default function ReportTabs({ tabs, activeId, onChange }) {
  return (
    <div
      role="tablist"
      aria-label="Report"
      className="inline-flex flex-wrap gap-1 rounded-md border border-surface-border bg-surface-card p-1"
    >
      {tabs.map((tab) => {
        const active = tab.id === activeId
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(tab.id)}
            className={[
              'h-8 rounded px-3 text-sm font-semibold transition-colors',
              active
                ? 'bg-brand-700 text-white'
                : 'text-ink-muted hover:bg-surface-muted hover:text-ink',
            ].join(' ')}
          >
            {tab.label}
          </button>
        )
      })}
    </div>
  )
}
