import { useMemo, useState } from 'react'

import StatCard from '../../../components/ui/StatCard.jsx'
import { formatCurrency, formatNumber } from '../../../lib/format.js'
import SortHeader from './SortHeader.jsx'
import { nextSort, applySort } from '../tableSort.js'
import Pagination from '../../../components/ui/Pagination.jsx'
import usePagination from '../../../hooks/usePagination.js'

/**
 * Report 4 — Inventory Valuation. Point-in-time monetary value of current stock
 * (`quantityAvailable × unitCost` per item). NOT date-range filtered.
 *
 * Data source: `getInventory()` (frontend/src/api/inventory.js) → `{ items, summary }`,
 * the live `GET /api/v1/inventory`. Each item is `InventoryRowDto`
 * { itemId, itemName, quantityAvailable, reorderLevel, unitCost, status, rowVersion, supplierId?, supplierName? }.
 *
 * DECISION — no Category column. `InventoryRowDto` carries no category field (only the
 * reports data does). Rather than fabricate categories by parsing item names, this view
 * shows a single "Item" column. Revisit if `GET /api/v1/inventory` adds `categoryName`.
 *
 * No chart — a valuation ledger is a snapshot, not a trend.
 */

const STATUS_STYLES = {
  // The design-system `status` tokens are dark/grey/red; this report follows the
  // brief's explicit green / amber / red intent using Tailwind's default palette.
  OK: { label: 'OK', className: 'bg-emerald-100 text-emerald-800' },
  WATCH: { label: 'WATCH', className: 'bg-amber-100 text-amber-800' },
  REORDER_NOW: { label: 'REORDER NOW', className: 'bg-red-100 text-red-700' },
}

function StockStatusBadge({ status }) {
  const style = STATUS_STYLES[status] ?? { label: status, className: 'bg-surface-muted text-ink' }
  return (
    <span
      className={`inline-flex whitespace-nowrap rounded px-2 py-1 text-xs font-semibold ${style.className}`}
    >
      {style.label}
    </span>
  )
}

const ACCESSORS = {
  itemName: (r) => r.itemName,
  quantityAvailable: (r) => r.quantityAvailable,
  unitCost: (r) => r.unitCost,
  totalValue: (r) => r.quantityAvailable * r.unitCost,
  status: (r) => r.status,
}

export default function InventoryValuationView({ items }) {
  const [sort, setSort] = useState({ key: 'totalValue', dir: 'desc' })
  const onSort = (key) => setSort((current) => nextSort(current, key))

  const summary = useMemo(() => {
    const totalValue = items.reduce((sum, it) => sum + it.quantityAvailable * it.unitCost, 0)
    return {
      totalValue,
      inStock: items.filter((it) => it.quantityAvailable > 0).length,
      needingReorder: items.filter((it) => it.status === 'REORDER_NOW').length,
    }
  }, [items])

  const sorted = applySort(items, sort, ACCESSORS)
  const { page, setPage, totalPages, total, isOnPage } = usePagination(sorted)
  const totalValue = summary.totalValue

  return (
    <div className="space-y-6 p-4">
      <div className="grid grid-cols-1 gap-5 sm:grid-cols-3">
        <StatCard label="Total Stock Value" value={formatCurrency(summary.totalValue)} />
        <StatCard label="Items in Stock" value={formatNumber(summary.inStock)} />
        <StatCard label="Items Needing Reorder" value={formatNumber(summary.needingReorder)} />
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-surface-border text-xs font-semibold">
              <SortHeader label="Item" sortKey="itemName" sort={sort} onSort={onSort} />
              <SortHeader label="Qty Available" sortKey="quantityAvailable" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Unit Cost" sortKey="unitCost" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Total Value" sortKey="totalValue" sort={sort} onSort={onSort} align="right" />
              <SortHeader label="Stock Status" sortKey="status" sort={sort} onSort={onSort} />
            </tr>
          </thead>
          <tbody>
            {sorted.map((item, index) => (
              <tr
                key={item.itemId}
                className={`border-b border-surface-border last:border-0 ${
                  isOnPage(index) ? '' : 'hidden print:table-row'
                }`}
              >
                <td className="px-4 py-3 font-medium text-ink">{item.itemName}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink">
                  {formatNumber(item.quantityAvailable)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-muted">
                  {formatCurrency(item.unitCost)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums font-semibold text-ink">
                  {formatCurrency(item.quantityAvailable * item.unitCost)}
                </td>
                <td className="px-4 py-3">
                  <StockStatusBadge status={item.status} />
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-surface-border font-semibold text-ink">
              <td className="px-4 py-3" colSpan={3}>Total Stock Value</td>
              <td className="px-4 py-3 text-right tabular-nums">{formatCurrency(totalValue)}</td>
              <td className="px-4 py-3" />
            </tr>
          </tfoot>
        </table>
        <Pagination page={page} totalPages={totalPages} total={total} onPageChange={setPage} />
      </div>
    </div>
  )
}
