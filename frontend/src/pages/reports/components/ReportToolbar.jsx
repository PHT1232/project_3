import SearchInput from '../../../components/ui/SearchInput.jsx'
import Button from '../../../components/ui/Button.jsx'
import { ANY, COST_BUCKETS, isDefaultReportFilters } from '../reportFilters.js'

const selectClass =
  'h-10 rounded-md border border-surface-border bg-surface-card px-3 text-sm text-ink focus:border-brand-500 focus:outline-none'

/**
 * Search + dropdown filters shown above the per-item report list (Cost by Item,
 * Cost & Headcount). Filtering is client-side over the rows the report returned.
 */
export default function ReportToolbar({ value, categories, onChange, resultCount, totalCount }) {
  const dirty = !isDefaultReportFilters(value)

  return (
    <div className="mb-4 flex flex-col gap-3 rounded-card border border-surface-border bg-surface-muted p-3 sm:flex-row sm:flex-wrap sm:items-center">
      <SearchInput
        value={value.search}
        onChange={(search) => onChange({ ...value, search })}
        placeholder="Search items..."
        label="Search report items"
        className="w-full sm:w-56"
      />

      <label className="flex items-center gap-2 text-sm text-ink-muted">
        <span className="whitespace-nowrap">Category</span>
        <select
          value={value.category}
          onChange={(e) => onChange({ ...value, category: e.target.value })}
          className={selectClass}
        >
          <option value={ANY}>All categories</option>
          {categories.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
      </label>

      <label className="flex items-center gap-2 text-sm text-ink-muted">
        <span className="whitespace-nowrap">Approved cost</span>
        <select
          value={value.costBucket}
          onChange={(e) => onChange({ ...value, costBucket: e.target.value })}
          className={selectClass}
        >
          {COST_BUCKETS.map((bucket) => (
            <option key={bucket.id} value={bucket.id}>
              {bucket.label}
            </option>
          ))}
        </select>
      </label>

      <div className="flex items-center gap-3 sm:ml-auto">
        <span className="text-xs text-ink-muted">
          {resultCount} of {totalCount} items
        </span>
        {dirty && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() =>
              onChange({ search: '', category: ANY, costBucket: ANY })
            }
          >
            Clear
          </Button>
        )}
      </div>
    </div>
  )
}
