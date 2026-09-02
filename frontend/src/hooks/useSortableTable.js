import { useMemo, useState } from 'react'

/**
 * Shared click-to-sort behaviour for every table in the app, so sorting looks and behaves the
 * same everywhere instead of each table rolling its own.
 *
 * Column config maps a sort key to how its values compare:
 *
 *   {
 *     itemName:          { type: 'string' },
 *     quantityAvailable: { type: 'number' },
 *     isActive:          { type: 'boolean' },                          // true first
 *     status:            { type: 'order', order: ['REORDER_NOW', …] }, // explicit severity
 *     categoryName:      { type: 'string', value: (row) => row.categoryName ?? '' },
 *   }
 *
 * `value` is optional and defaults to `row[key]`. A column may instead supply its own
 * `compare(a, b)` for anything these types don't cover.
 *
 * Sorting is applied to whatever rows are passed in, so it only tells the truth when the caller
 * holds the full set. Do not use it on a server-paginated table — it would sort just the visible
 * page while appearing to sort everything.
 */

const TYPE_COMPARATORS = {
  string: (a, b) => String(a).localeCompare(String(b)),
  number: (a, b) => Number(a) - Number(b),
  // Active/enabled first, matching how the tables read.
  boolean: (a, b) => Number(Boolean(b)) - Number(Boolean(a)),
}

function isBlank(value) {
  return value === null || value === undefined || value === ''
}

function comparatorFor(column) {
  if (typeof column.compare === 'function') return column.compare

  if (column.type === 'order') {
    const order = column.order ?? []
    // Values outside the list sort after the known ones rather than throwing off the order.
    const rank = (value) => {
      const index = order.indexOf(value)
      return index === -1 ? order.length : index
    }
    return (a, b) => rank(a) - rank(b)
  }

  return TYPE_COMPARATORS[column.type] ?? TYPE_COMPARATORS.string
}

export default function useSortableTable(rows, columns, initialSort = null) {
  const [sort, setSort] = useState(initialSort)

  function toggleSort(key) {
    setSort((current) =>
      current && current.key === key
        ? { key, dir: current.dir === 'asc' ? 'desc' : 'asc' }
        : { key, dir: 'asc' },
    )
  }

  const sortedRows = useMemo(() => {
    if (!sort) return rows

    const column = columns[sort.key]
    if (!column) return rows

    const readValue = column.value ?? ((row) => row[sort.key])
    const compare = comparatorFor(column)
    const direction = sort.dir === 'desc' ? -1 : 1

    // Array.prototype.sort is stable, so rows comparing equal keep their incoming order.
    return [...rows].sort((rowA, rowB) => {
      const a = readValue(rowA)
      const b = readValue(rowB)

      // Blanks always sink to the bottom, whichever direction is active — a column of empty
      // cells at the top reads as broken data rather than as a sort result.
      const aBlank = isBlank(a)
      const bBlank = isBlank(b)
      if (aBlank && bBlank) return 0
      if (aBlank) return 1
      if (bBlank) return -1

      return compare(a, b) * direction
    })
  }, [rows, columns, sort])

  /** Spread onto <SortableHeader> so a table never wires the plumbing by hand. */
  function headerProps(key) {
    return {
      sortKey: key,
      activeKey: sort?.key ?? null,
      direction: sort?.dir ?? null,
      onSort: toggleSort,
    }
  }

  return { sortedRows, sort, toggleSort, headerProps }
}
