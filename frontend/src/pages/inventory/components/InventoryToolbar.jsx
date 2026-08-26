import { Filter } from 'lucide-react'
import Card from '../../../components/ui/Card.jsx'
import SearchInput from '../../../components/ui/SearchInput.jsx'
import { INVENTORY_STATUS } from '../../../api/inventory.js'

export const SORT_OPTIONS = [
  { id: 'NAME_ASC', label: 'Item Name (A–Z)' },
  { id: 'NAME_DESC', label: 'Item Name (Z–A)' },
  { id: 'STOCK_ASC', label: 'Current Stock (Low–High)' },
  { id: 'STOCK_DESC', label: 'Current Stock (High–Low)' },
]

/*
  The wireframe shows a "Filter" control but does not say what it filters. Status is the only
  filterable dimension actually rendered in the table, so this narrows by status. Flagged as an
  interpretation, not a documented requirement — change freely if the team decides otherwise.
*/
const STATUS_FILTERS = [
  { id: 'ALL', label: 'All statuses' },
  { id: INVENTORY_STATUS.OK, label: 'OK' },
  { id: INVENTORY_STATUS.WATCH, label: 'Watch' },
  { id: INVENTORY_STATUS.REORDER_NOW, label: 'Reorder now' },
]

const selectClass =
  'h-10 rounded-md border border-surface-border bg-surface-card px-3 text-sm text-ink'

export default function InventoryToolbar({ searchTerm, onSearch, status, onStatus, sort, onSort }) {
  return (
    <Card className="mb-4 flex flex-col gap-3 p-3 lg:flex-row lg:items-center">
      <SearchInput
        value={searchTerm}
        onChange={onSearch}
        placeholder="Search by Item Name or SKU..."
        label="Search inventory by item name or SKU"
        className="lg:max-w-sm lg:flex-1"
      />

      <div className="flex flex-wrap items-center gap-3 lg:ml-auto">
        <div className="flex items-center gap-2">
          <Filter className="h-4 w-4 text-ink-muted" aria-hidden="true" />
          <select
            value={status}
            aria-label="Filter by stock status"
            onChange={(e) => onStatus(e.target.value)}
            className={selectClass}
          >
            {STATUS_FILTERS.map((option) => (
              <option key={option.id} value={option.id}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-2">
          <label htmlFor="inventory-sort" className="text-sm text-ink-muted">
            Sort by:
          </label>
          <select
            id="inventory-sort"
            value={sort}
            onChange={(e) => onSort(e.target.value)}
            className={selectClass}
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.id} value={option.id}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
      </div>
    </Card>
  )
}
