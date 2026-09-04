import { useState } from 'react'

import { formatCurrency } from '../../../lib/format.js'
import DonutChart from './charts/DonutChart.jsx'
import SortHeader from './SortHeader.jsx'
import { nextSort, applySort } from '../tableSort.js'
import Pagination from '../../../components/ui/Pagination.jsx'
import usePagination from '../../../hooks/usePagination.js'

/**
 * Report 1 — approved spend per item and each item's share of the total.
 * Donut shows the part-to-whole (always ranked by cost, order-independent); the
 * table carries the exact figures and its columns are click-to-sort. The footer
 * reconciles to 100.00% over the full result set (page-map §9 / TC-16); once the
 * toolbar filters the list, it shows the shown subset instead.
 *
 * `rows` arrives already filtered by the page (`applyReportFilters`); this view
 * only re-orders it.
 */
const ACCESSORS = {
  itemName: (r) => r.itemName,
  categoryName: (r) => r.categoryName,
  approvedCost: (r) => r.approvedCost,
  percentOfTotal: (r) => r.percentOfTotal,
}

export default function CostByItemView({ rows, totalApprovedCost, filtered }) {
  const [sort, setSort] = useState(null)
  const sorted = applySort(rows, sort, ACCESSORS)
  const { page, setPage, totalPages, total, isOnPage } = usePagination(sorted)
  const onSort = (key) => setSort((current) => nextSort(current, key))

  const shownCost = rows.reduce((sum, row) => sum + row.approvedCost, 0)
  const shownPercent = rows.reduce((sum, row) => sum + row.percentOfTotal, 0)

  return (
    <div className="space-y-6 p-4">
      <div data-print-hide>
        <DonutChart
          slices={rows.map((row) => ({ label: row.itemName, value: row.approvedCost }))}
          format={formatCurrency}
          ariaLabel="Share of approved spend by item"
        />
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-surface-border text-xs font-semibold">
              <SortHeader label="Item" sortKey="itemName" sort={sort} onSort={onSort} />
              <SortHeader label="Category" sortKey="categoryName" sort={sort} onSort={onSort} />
              <SortHeader label="Approved Cost" sortKey="approvedCost" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="% of Total" sortKey="percentOfTotal" sort={sort} onSort={onSort} align="right" />
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
                <td className="px-4 py-3">
                  <div className="flex items-center justify-end gap-3">
                    <div
                      className="h-2 w-28 shrink-0 overflow-hidden rounded bg-surface-muted"
                      aria-hidden="true"
                      data-print-hide
                    >
                      <div
                        className="h-full rounded bg-brand-600"
                        style={{ width: `${Math.min(row.percentOfTotal, 100)}%` }}
                      />
                    </div>
                    <span className="w-14 text-right tabular-nums font-semibold text-ink">
                      {row.percentOfTotal.toFixed(2)}%
                    </span>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-surface-border font-semibold text-ink">
              <td className="px-4 py-3" colSpan={2}>{filtered ? `Shown (${rows.length})` : 'Total'}</td>
              <td className="px-4 py-3 text-right tabular-nums">{formatCurrency(shownCost)}</td>
              <td className="px-4 py-3 text-right tabular-nums">{shownPercent.toFixed(2)}%</td>
            </tr>
          </tfoot>
        </table>
        <Pagination page={page} totalPages={totalPages} total={total} onPageChange={setPage} />
        {filtered && (
          <p className="px-4 py-3 text-xs text-ink-muted">
            Percentages are each item’s share of the full approved spend
            ({formatCurrency(totalApprovedCost)}); the filtered rows above sum to a subset.
          </p>
        )}
      </div>
    </div>
  )
}
