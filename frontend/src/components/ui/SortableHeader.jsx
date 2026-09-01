import { ChevronUp, ChevronDown, ChevronsUpDown } from 'lucide-react'

/**
 * A clickable table header cell. Pair with useSortableTable:
 *
 *   <SortableHeader {...headerProps('itemName')}>Item Name</SortableHeader>
 *
 * Accessibility: the <th> carries aria-sort so screen readers announce the active column and
 * direction, and the trigger is a real <button>, which gives keyboard focus, Enter/Space and the
 * app's focus ring for free — no manual key handling.
 */
export default function SortableHeader({
  children,
  sortKey,
  activeKey,
  direction,
  onSort,
  align = 'left',
  className = '',
}) {
  const active = activeKey === sortKey
  const ariaSort = active ? (direction === 'desc' ? 'descending' : 'ascending') : 'none'
  const ActiveIcon = direction === 'desc' ? ChevronDown : ChevronUp

  return (
    <th
      scope="col"
      aria-sort={ariaSort}
      className={`px-4 py-3 ${align === 'right' ? 'text-right' : 'text-left'} ${className}`}
    >
      <button
        type="button"
        onClick={() => onSort(sortKey)}
        className={`group inline-flex items-center gap-1 rounded ${
          align === 'right' ? 'flex-row-reverse' : ''
        } hover:text-ink focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500`}
      >
        <span>{children}</span>
        {active ? (
          <ActiveIcon className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
        ) : (
          // Faint neutral hint that the column is sortable — invisible until hover/focus so the
          // header row stays quiet.
          <ChevronsUpDown
            className="h-3.5 w-3.5 shrink-0 opacity-0 transition-opacity group-hover:opacity-40 group-focus-visible:opacity-40"
            aria-hidden="true"
          />
        )}
      </button>
    </th>
  )
}
