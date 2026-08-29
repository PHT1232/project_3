import { sortIndicator } from '../tableSort.js'

/**
 * A `<th>` whose label is a button that cycles the table sort for `sortKey`.
 * `sort` is the current `{ key, dir } | null`; `onSort(key)` advances the cycle.
 * `align="right"` for numeric columns.
 */
export default function SortHeader({ label, sortKey, sort, onSort, align = 'left' }) {
  const active = sort?.key === sortKey
  return (
    <th scope="col" className={`px-4 py-3 ${align === 'right' ? 'text-right' : 'text-left'}`}>
      <button
        type="button"
        onClick={() => onSort(sortKey)}
        aria-label={`Sort by ${label}`}
        className={[
          'inline-flex items-center gap-1 uppercase tracking-wide transition-colors hover:text-ink',
          align === 'right' ? 'flex-row-reverse' : '',
          active ? 'text-ink' : 'text-ink-muted',
        ].join(' ')}
      >
        {label}
        <span aria-hidden="true" className={active ? 'text-brand-600' : 'text-ink-subtle'}>
          {sortIndicator(sort, sortKey)}
        </span>
      </button>
    </th>
  )
}
