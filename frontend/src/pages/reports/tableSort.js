/**
 * Shared 3-state column sorting for the report tables (Cost by Item, Cost & Headcount,
 * Inventory Valuation, By Team). Each view keeps its own `sort` state via `useState`;
 * these helpers just describe the cycle and apply it.
 *
 * A `sort` value is `null` (unsorted) or `{ key: string, dir: 'asc' | 'desc' }`.
 * Cycle on click: unsorted → desc → asc → unsorted.
 */

export function nextSort(current, key) {
  if (!current || current.key !== key) return { key, dir: 'desc' }
  if (current.dir === 'desc') return { key, dir: 'asc' }
  return null
}

/** '↕' when this column is inactive, '↓' for desc, '↑' for asc. */
export function sortIndicator(sort, key) {
  if (!sort || sort.key !== key) return '↕'
  return sort.dir === 'desc' ? '↓' : '↑'
}

/**
 * Return a sorted copy of `rows`. `accessors[key]` returns the comparable value for
 * a row; string values sort alphabetically (locale-aware), everything else numerically.
 * With no active sort the input order is preserved.
 */
export function applySort(rows, sort, accessors) {
  if (!sort) return rows
  const get = accessors[sort.key]
  if (!get) return rows

  const factor = sort.dir === 'asc' ? 1 : -1
  return [...rows].sort((a, b) => {
    const av = get(a)
    const bv = get(b)
    if (typeof av === 'string' || typeof bv === 'string') {
      return String(av).localeCompare(String(bv)) * factor
    }
    return (av - bv) * factor
  })
}
