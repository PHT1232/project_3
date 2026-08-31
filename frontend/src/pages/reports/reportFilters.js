/**
 * Client-side search + filter over the per-item report rows (Cost by Item,
 * Cost & Headcount). Pure so it is trivially testable and the view stays declarative.
 */

export const ANY = 'ANY'

/** Approved-cost bands for the dropdown. `test(cost)` decides membership. */
export const COST_BUCKETS = [
  { id: ANY, label: 'Any cost', test: () => true },
  { id: 'LT_100', label: 'Under $100', test: (c) => c < 100 },
  { id: '100_500', label: '$100 – $500', test: (c) => c >= 100 && c < 500 },
  { id: '500_1000', label: '$500 – $1,000', test: (c) => c >= 500 && c < 1000 },
  { id: 'GTE_1000', label: '$1,000 and over', test: (c) => c >= 1000 },
]

export const DEFAULT_REPORT_FILTERS = { search: '', category: ANY, costBucket: ANY }

export function isDefaultReportFilters(filters) {
  return (
    filters.search.trim() === '' && filters.category === ANY && filters.costBucket === ANY
  )
}

/** Distinct category names present in the current rows, for the category dropdown. */
export function categoryOptions(rows) {
  return [...new Set(rows.map((row) => row.categoryName))].sort((a, b) => a.localeCompare(b))
}

export function applyReportFilters(rows, filters) {
  const term = filters.search.trim().toLowerCase()
  const bucket = COST_BUCKETS.find((b) => b.id === filters.costBucket) ?? COST_BUCKETS[0]

  return rows.filter((row) => {
    if (term && !row.itemName.toLowerCase().includes(term)) return false
    if (filters.category !== ANY && row.categoryName !== filters.category) return false
    if (!bucket.test(row.approvedCost)) return false
    return true
  })
}
