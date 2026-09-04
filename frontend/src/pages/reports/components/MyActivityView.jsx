import { useState } from 'react'

import { formatCurrency, formatNumber } from '../../../lib/format.js'
import LineChart from './charts/LineChart.jsx'
import SortHeader from './SortHeader.jsx'
import { nextSort, applySort } from '../tableSort.js'
import Pagination from '../../../components/ui/Pagination.jsx'
import usePagination from '../../../hooks/usePagination.js'

/**
 * "My Requests" — always the signed-in user's own approved activity, regardless of role.
 * Shown as its own tab so a manager who is also a requestor can see their personal spend
 * separately from their team/group reports (GET /api/v1/reports/my-activity).
 */
const ACCESSORS = {
  itemName: (r) => r.itemName,
  categoryName: (r) => r.categoryName,
  approvedCost: (r) => r.approvedCost,
  unitsApproved: (r) => r.unitsApproved,
}

export default function MyActivityView({ report }) {
  const { rows, points, approvedCost } = report
  const [sort, setSort] = useState(null)
  const sorted = applySort(rows, sort, ACCESSORS)
  const { page, setPage, totalPages, total, isOnPage } = usePagination(sorted)
  const onSort = (key) => setSort((current) => nextSort(current, key))

  const monthly = points.map((p) => ({ label: p.periodLabel, value: p.periodCost }))

  return (
    <div className="space-y-6 p-4">
      {points.length > 0 && (
        <div data-print-hide>
          <h3 className="mb-2 text-sm font-semibold text-ink">Your approved spend per month</h3>
          <LineChart points={monthly} format={formatCurrency} ariaLabel="Your approved spend per month" />
        </div>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-surface-border text-xs font-semibold">
              <SortHeader label="Item" sortKey="itemName" sort={sort} onSort={onSort} />
              <SortHeader label="Category" sortKey="categoryName" sort={sort} onSort={onSort} />
              <SortHeader label="Approved Cost" sortKey="approvedCost" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Units" sortKey="unitsApproved" sort={sort} onSort={onSort} align="right" />
            </tr>
          </thead>
          <tbody>
            {sorted.map((row, index) => (
              <tr
                key={row.itemId}
                className={`border-b border-surface-border last:border-0 ${
                  isOnPage(index) ? '' : 'hidden print:table-row'
                }`}
              >
                <td className="px-4 py-3 font-medium text-ink">{row.itemName}</td>
                <td className="px-4 py-3 text-ink-muted">{row.categoryName}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatCurrency(row.approvedCost)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatNumber(row.unitsApproved)}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-surface-border font-semibold text-ink">
              <td className="px-4 py-3" colSpan={2}>Total</td>
              <td className="px-4 py-3 text-right tabular-nums">{formatCurrency(approvedCost)}</td>
              <td className="px-4 py-3" />
            </tr>
          </tfoot>
        </table>
        <Pagination page={page} totalPages={totalPages} total={total} onPageChange={setPage} noun="request" />
      </div>
    </div>
  )
}
