import { useEffect, useMemo, useState } from 'react'

/** Rows per page for every client-side paginated table in the app. */
export const DEFAULT_PAGE_SIZE = 12

/**
 * Client-side pagination for a table that already has all its rows in memory.
 *
 * Two ways to consume it, because the tables have different constraints:
 *  - `pageRows` — render just the current slice. Use this when the table is only ever read
 *    on screen (Inventory, Item Management, Suppliers).
 *  - `isOnPage(index)` — render every row and hide the off-page ones with
 *    `hidden print:table-row`. The Reports tabs need this: the Print button prints the live
 *    DOM, so a sliced table would print page 1 only and silently truncate a cost report.
 *
 * The page resets to 1 when the number of rows changes, which covers filtering and searching.
 * A filter that happens to leave the count unchanged keeps the current page; the clamp below
 * still guarantees the page is never past the end.
 */
export default function usePagination(rows, pageSize = DEFAULT_PAGE_SIZE) {
  const [page, setPage] = useState(1)
  const total = rows.length
  const totalPages = Math.max(1, Math.ceil(total / pageSize))

  useEffect(() => {
    setPage(1)
  }, [total])

  useEffect(() => {
    if (page > totalPages) setPage(totalPages)
  }, [page, totalPages])

  const safePage = Math.min(page, totalPages)
  const start = (safePage - 1) * pageSize
  const end = start + pageSize

  const pageRows = useMemo(() => rows.slice(start, end), [rows, start, end])

  return {
    page: safePage,
    setPage,
    totalPages,
    total,
    pageRows,
    isOnPage: (index) => index >= start && index < end,
  }
}
