import { useState } from 'react'

import { formatCurrency, formatNumber } from '../../../lib/format.js'
import BarChart from './charts/BarChart.jsx'
import SortHeader from './SortHeader.jsx'
import { nextSort, applySort } from '../tableSort.js'

const TOP_N = 8

/**
 * Report 2 — approved spend per item, how many DISTINCT employees requested it
 * (page-map §9 / TC-17), and total units approved. The bar chart ranks the most
 * requested items by units; the table columns are click-to-sort.
 *
 * `rows` arrives already filtered by the page; this view only re-orders it.
 */
const ACCESSORS = {
  itemName: (r) => r.itemName,
  categoryName: (r) => r.categoryName,
  approvedCost: (r) => r.approvedCost,
  unitsApproved: (r) => r.unitsApproved,
  requestorCount: (r) => r.requestorCount,
  requestCount: (r) => r.requestCount,
}

export default function ItemHeadcountView({ rows, totalApprovedCost, filtered }) {
  const [sort, setSort] = useState(null)
  const sorted = applySort(rows, sort, ACCESSORS)
  const onSort = (key) => setSort((current) => nextSort(current, key))

  const topByUnits = [...rows]
    .sort((a, b) => b.unitsApproved - a.unitsApproved)
    .slice(0, TOP_N)
    .map((row) => ({ label: row.itemName, value: row.unitsApproved }))

  return (
    <div className="space-y-6 p-4">
      <div data-print-hide>
        <h3 className="mb-3 text-sm font-semibold text-ink">
          Most requested items by units{filtered ? ' (filtered)' : ''}
        </h3>
        <BarChart bars={topByUnits} format={formatNumber} ariaLabel="Most requested items by units approved" />
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-surface-border text-xs font-semibold">
              <SortHeader label="Item" sortKey="itemName" sort={sort} onSort={onSort} />
              <SortHeader label="Category" sortKey="categoryName" sort={sort} onSort={onSort} />
              <SortHeader label="Approved Cost" sortKey="approvedCost" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Units" sortKey="unitsApproved" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Requestors" sortKey="requestorCount" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Requests" sortKey="requestCount" sort={sort} onSort={onSort} align="right" />
            </tr>
          </thead>
          <tbody>
            {sorted.map((row) => (
              <tr key={row.itemId} className="border-b border-surface-border last:border-0">
                <td className="px-4 py-3 font-medium text-ink">{row.itemName}</td>
                <td className="px-4 py-3 text-ink-muted">{row.categoryName}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatCurrency(row.approvedCost)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatNumber(row.unitsApproved)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatNumber(row.requestorCount)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-muted">
                  {formatNumber(row.requestCount)}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-surface-border font-semibold text-ink">
              <td className="px-4 py-3" colSpan={2}>{filtered ? `Shown (${rows.length})` : 'Total'}</td>
              <td className="px-4 py-3 text-right tabular-nums">
                {formatCurrency(rows.reduce((sum, row) => sum + row.approvedCost, 0))}
              </td>
              <td className="px-4 py-3" colSpan={3} />
            </tr>
          </tfoot>
        </table>
        {filtered && (
          <p className="px-4 py-3 text-xs text-ink-muted">
            Full approved spend for the period: {formatCurrency(totalApprovedCost)}.
          </p>
        )}
      </div>
    </div>
  )
}
