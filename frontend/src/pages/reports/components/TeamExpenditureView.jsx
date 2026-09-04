import { useState } from 'react'

import { formatCurrency, formatNumber } from '../../../lib/format.js'
import BarChart from './charts/BarChart.jsx'
import SortHeader from './SortHeader.jsx'
import { nextSort, applySort } from '../tableSort.js'
import Pagination from '../../../components/ui/Pagination.jsx'
import usePagination from '../../../hooks/usePagination.js'

/**
 * Report 5 — Expenditure by Team. Approved spend grouped by the requestor's
 * approving manager ("which manager's team is spending the most?"). Date-range
 * filtered like the other cost reports.
 *
 * `report.rows`: { teamName, memberCount, requestCount, approvedCost, percentOfTotal }
 * — already sorted by cost desc; this view re-orders on column click.
 */
const ACCESSORS = {
  teamName: (r) => r.teamName,
  memberCount: (r) => r.memberCount,
  requestCount: (r) => r.requestCount,
  approvedCost: (r) => r.approvedCost,
  percentOfTotal: (r) => r.percentOfTotal,
}

export default function TeamExpenditureView({ report }) {
  const { rows, totalApprovedCost } = report
  const [sort, setSort] = useState(null)
  const onSort = (key) => setSort((current) => nextSort(current, key))
  const sorted = applySort(rows, sort, ACCESSORS)
  const { page, setPage, totalPages, total, isOnPage } = usePagination(sorted)

  const totalMembers = rows.reduce((sum, row) => sum + row.memberCount, 0)

  return (
    <div className="space-y-6 p-4">
      <div data-print-hide>
        <h3 className="mb-3 text-sm font-semibold text-ink">Approved spend by team</h3>
        <BarChart
          bars={rows.map((row) => ({ label: row.teamName, value: row.approvedCost }))}
          format={formatCurrency}
          ariaLabel="Approved spend by team"
        />
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-surface-border text-xs font-semibold">
              <SortHeader label="Team (Manager)" sortKey="teamName" sort={sort} onSort={onSort} />
              <SortHeader label="Members" sortKey="memberCount" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Requests" sortKey="requestCount" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Approved Cost" sortKey="approvedCost" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="% of Total" sortKey="percentOfTotal" sort={sort} onSort={onSort} align="right" />
            </tr>
          </thead>
          <tbody>
            {sorted.map((row, index) => (
              <tr
                key={row.teamName}
                className={`border-b border-surface-border last:border-0 ${
                  isOnPage(index) ? '' : 'hidden print:table-row'
                }`}
              >
                <td className="px-4 py-3 font-medium text-ink">{row.teamName}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatNumber(row.memberCount)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-muted">
                  {formatNumber(row.requestCount)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatCurrency(row.approvedCost)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums font-semibold text-ink">
                  {row.percentOfTotal.toFixed(2)}%
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-surface-border font-semibold text-ink">
              <td className="px-4 py-3">Total</td>
              <td className="px-4 py-3 text-right tabular-nums">{formatNumber(totalMembers)}</td>
              <td className="px-4 py-3" />
              <td className="px-4 py-3 text-right tabular-nums">{formatCurrency(totalApprovedCost)}</td>
              <td className="px-4 py-3 text-right tabular-nums">100.00%</td>
            </tr>
          </tfoot>
        </table>
        <Pagination page={page} totalPages={totalPages} total={total} onPageChange={setPage} noun="team" />
      </div>
    </div>
  )
}
