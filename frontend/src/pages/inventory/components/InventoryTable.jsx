import { useState } from 'react'
import { MoreVertical } from 'lucide-react'

import { formatNumber } from '../../../lib/format.js'
import StockStatusBadge from './StockStatusBadge.jsx'

function RowMenu({ row, onAdjust, onReceive }) {
  const [open, setOpen] = useState(false)

  return (
    <div className="relative">
      <button
        type="button"
        aria-label={`Actions for ${row.itemName}`}
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
        className="rounded p-1.5 text-ink-muted hover:bg-surface-muted hover:text-ink"
      >
        <MoreVertical className="h-4 w-4" />
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} aria-hidden="true" />
          <div className="absolute right-0 z-20 mt-1 w-44 overflow-hidden rounded-md border border-surface-border bg-surface-card py-1 shadow-lg">
            <button
              type="button"
              onClick={() => {
                setOpen(false)
                onAdjust(row)
              }}
              className="block w-full px-3 py-2 text-left text-sm text-ink hover:bg-surface-muted"
            >
              Adjust stock
            </button>
            <button
              type="button"
              onClick={() => {
                setOpen(false)
                onReceive(row)
              }}
              className="block w-full px-3 py-2 text-left text-sm text-ink hover:bg-surface-muted"
            >
              Receive goods
            </button>
          </div>
        </>
      )}
    </div>
  )
}

/**
 * Inventory table. Column order matches the approved wireframe:
 * select · Item Name · SKU · Current Stock · Reorder Level · Status · row actions.
 *
 * Row selection is tracked because the wireframe shows checkboxes, but no bulk action is
 * offered — the Plan specifies none (K5 in CLAUDE.md §6). The selection count is surfaced so
 * the control is not inert, and a bulk action can be added once one is specified.
 */
export default function InventoryTable({ rows, selectedIds, onToggle, onToggleAll, onAdjust, onReceive }) {
  const allSelected = rows.length > 0 && rows.every((row) => selectedIds.includes(row.itemId))

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[760px] border-collapse text-sm">
        <thead>
          <tr className="border-b border-surface-border bg-surface-muted text-left">
            <th scope="col" className="w-10 px-4 py-3">
              <input
                type="checkbox"
                checked={allSelected}
                onChange={onToggleAll}
                aria-label="Select all rows"
                className="h-4 w-4 rounded border-surface-border text-brand-700 focus:ring-brand-500"
              />
            </th>
            <th scope="col" className="px-4 py-3 font-semibold text-ink">Item Name</th>
            <th scope="col" className="px-4 py-3 font-semibold text-ink">SKU</th>
            <th scope="col" className="px-4 py-3 text-right font-semibold text-ink">Current Stock</th>
            <th scope="col" className="px-4 py-3 text-right font-semibold text-ink">Reorder Level</th>
            <th scope="col" className="px-4 py-3 font-semibold text-ink">Status</th>
            <th scope="col" className="w-12 px-4 py-3">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const selected = selectedIds.includes(row.itemId)
            return (
              <tr
                key={row.itemId}
                className="border-b border-surface-border last:border-0 hover:bg-surface-muted/60"
              >
                <td className="px-4 py-3">
                  <input
                    type="checkbox"
                    checked={selected}
                    onChange={() => onToggle(row.itemId)}
                    aria-label={`Select ${row.itemName}`}
                    className="h-4 w-4 rounded border-surface-border text-brand-700 focus:ring-brand-500"
                  />
                </td>
                <td className="px-4 py-3 text-ink">{row.itemName}</td>
                <td className="px-4 py-3 font-mono text-xs text-brand-600">{row.sku}</td>
                <td className="px-4 py-3 text-right font-semibold text-ink">
                  {formatNumber(row.quantityAvailable)}
                </td>
                <td className="px-4 py-3 text-right text-ink-muted">
                  {formatNumber(row.reorderLevel)}
                </td>
                <td className="px-4 py-3">
                  <StockStatusBadge status={row.status} />
                </td>
                <td className="px-4 py-3">
                  <RowMenu row={row} onAdjust={onAdjust} onReceive={onReceive} />
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
