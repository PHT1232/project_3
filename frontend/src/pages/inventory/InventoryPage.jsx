import { useMemo, useState } from 'react'
import { SlidersHorizontal, PackagePlus, ShoppingCart } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import StatCard from '../../components/ui/StatCard.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import useSortableTable from '../../hooks/useSortableTable.js'
import { getInventory, INVENTORY_STATUS } from '../../api/inventory.js'
import { formatCurrency, formatNumber } from '../../lib/format.js'

import InventoryToolbar from './components/InventoryToolbar.jsx'
import InventoryTable from './components/InventoryTable.jsx'
import StockActionModal from './components/StockActionModal.jsx'
import StockHistoryModal from './components/StockHistoryModal.jsx'
import SupplierRequestModal from './components/SupplierRequestModal.jsx'

/**
 * Sortable columns. The itemName/quantityAvailable comparators are the ones the old "Sort by"
 * dropdown used, unchanged — only the trigger moved to the column headers. Status is ordered by
 * severity rather than alphabetically, because A–Z would read "OK, REORDER_NOW, WATCH".
 */
const SORT_COLUMNS = {
  itemName: { type: 'string' },
  quantityAvailable: { type: 'number' },
  reorderLevel: { type: 'number' },
  status: {
    type: 'order',
    order: [INVENTORY_STATUS.REORDER_NOW, INVENTORY_STATUS.WATCH, INVENTORY_STATUS.OK],
  },
}

export default function InventoryPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [status, setStatus] = useState('ALL')
  const [selectedIds, setSelectedIds] = useState([])
  const [action, setAction] = useState({ mode: null, item: null })
  // The item whose stock ledger is open. Separate from `action` because viewing history is a
  // read, not a mutation — it shares no state with the Adjust/Receive dialog.
  const [historyItem, setHistoryItem] = useState(null)
  // The inventory cart: selectedIds is the membership, cartQuantities the per-item amount.
  // Both live above the filter/sort logic, so searching or re-sorting never drops the cart
  // (local useState only — the project deliberately has no Redux, Plan §2.4).
  const [cartQuantities, setCartQuantities] = useState({})
  const [cartOpen, setCartOpen] = useState(false)

  const { data, error, loading, reload } = useAsync(() => getInventory(), [])

  const rows = data?.items ?? []
  const summary = data?.summary

  const filteredRows = useMemo(() => {
    const term = searchTerm.trim().toLowerCase()
    return rows.filter((row) => {
      if (status !== 'ALL' && row.status !== status) return false
      if (!term) return true
      return (
        row.itemName.toLowerCase().includes(term) || (row.sku ?? '').toLowerCase().includes(term)
      )
    })
  }, [rows, searchTerm, status])

  const { sortedRows: visibleRows, headerProps } = useSortableTable(filteredRows, SORT_COLUMNS, {
    key: 'itemName',
    dir: 'asc',
  })

  function toggleRow(itemId) {
    setSelectedIds((current) =>
      current.includes(itemId) ? current.filter((id) => id !== itemId) : [...current, itemId],
    )
    setCartQuantities((current) => ({ ...current, [itemId]: current[itemId] ?? 1 }))
  }

  function toggleAll() {
    const visibleIds = visibleRows.map((row) => row.itemId)
    const allSelected = visibleIds.every((id) => selectedIds.includes(id))
    setSelectedIds(allSelected ? [] : visibleIds)
    if (!allSelected) {
      setCartQuantities((current) => {
        const next = { ...current }
        visibleIds.forEach((id) => {
          next[id] = next[id] ?? 1
        })
        return next
      })
    }
  }

  function removeFromCart(itemId) {
    setSelectedIds((current) => current.filter((id) => id !== itemId))
  }

  function clearCart() {
    setSelectedIds([])
    setCartQuantities({})
  }

  // Cart rows come from the full row set, not visibleRows — an item stays in the cart even once
  // a search or status filter hides its row.
  const cartRows = useMemo(
    () => selectedIds.map((id) => rows.find((row) => row.itemId === id)).filter(Boolean),
    [selectedIds, rows],
  )

  const hasFilters = searchTerm.trim() !== '' || status !== 'ALL'

  // The toolbar's Adjust / Receive act on the SELECTED row. They previously acted on
  // `visibleRows[0]` — whatever happened to sort first — so re-sorting or filtering silently
  // changed which item the button would modify, and the checkbox the user had ticked was
  // ignored. Both write to the stock ledger, so hitting the wrong item is a real correction to
  // make afterwards, not a cosmetic slip.
  //
  // Exactly one selection is required: the same `selectedIds` also feeds the multi-item supplier
  // cart, so with two or more ticked there is no single unambiguous target. The button is
  // disabled in that case and says why, rather than guessing.
  const singleSelectedRow = cartRows.length === 1 ? cartRows[0] : null
  const singleSelectionHint =
    cartRows.length === 0
      ? 'Select one item to adjust or receive stock'
      : cartRows.length > 1
        ? 'Select exactly one item — several are selected'
        : undefined

  return (
    <>
      <PageHeader
        title="Inventory / Stock Ledger"
        description="Real-time overview of all stationery items in stock."
        actions={
          <>
            <Button
              variant="secondary"
              onClick={() => setAction({ mode: 'adjust', item: singleSelectedRow })}
              disabled={!singleSelectedRow}
              title={singleSelectionHint}
            >
              <SlidersHorizontal className="h-4 w-4" aria-hidden="true" />
              Adjust Stock
            </Button>
            <Button
              variant="secondary"
              onClick={() => setAction({ mode: 'receive', item: singleSelectedRow })}
              disabled={!singleSelectedRow}
              title={singleSelectionHint}
            >
              <PackagePlus className="h-4 w-4" aria-hidden="true" />
              Receive Goods
            </Button>
            <Button
              onClick={() => setCartOpen(true)}
              disabled={cartRows.length === 0}
              title={
                cartRows.length === 0
                  ? 'Select one or more items to request from suppliers'
                  : undefined
              }
            >
              <ShoppingCart className="h-4 w-4" aria-hidden="true" />
              Request from Suppliers
              {cartRows.length > 0 && ` (${cartRows.length})`}
            </Button>
          </>
        }
      />

      {!loading && !error && summary && (
        <div className="mb-6 grid grid-cols-1 gap-5 sm:grid-cols-3">
          <StatCard label="Total Items" value={formatNumber(summary.totalItems)} />
          <StatCard label="Low Stock Alerts" value={formatNumber(summary.lowStockAlerts)} />
          <StatCard label="Total Value" value={formatCurrency(summary.totalValue)} />
        </div>
      )}

      {!loading && !error && (
        <InventoryToolbar
          searchTerm={searchTerm}
          onSearch={setSearchTerm}
          status={status}
          onStatus={setStatus}
        />
      )}

      <Card className="overflow-hidden">
        {loading && <LoadingState label="Loading inventory…" />}

        {!loading && error && <ErrorState error={error} onRetry={reload} />}

        {!loading && !error && visibleRows.length === 0 && (
          <EmptyState
            title={rows.length === 0 ? 'No stock records' : 'No items match your search'}
            description={
              rows.length === 0
                ? 'Once stationery items are stocked they will appear in the ledger.'
                : 'Try a different item name or SKU, or clear the status filter.'
            }
            action={
              hasFilters && (
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() => {
                    setSearchTerm('')
                    setStatus('ALL')
                  }}
                >
                  Clear filters
                </Button>
              )
            }
          />
        )}

        {!loading && !error && visibleRows.length > 0 && (
          <InventoryTable
            rows={visibleRows}
            selectedIds={selectedIds}
            onToggle={toggleRow}
            onToggleAll={toggleAll}
            onAdjust={(item) => setAction({ mode: 'adjust', item })}
            onReceive={(item) => setAction({ mode: 'receive', item })}
            onViewHistory={setHistoryItem}
            headerProps={headerProps}
          />
        )}
      </Card>

      {cartRows.length > 0 && (
        <p className="mt-3 text-sm text-ink-muted">
          {cartRows.length} item{cartRows.length === 1 ? '' : 's'} selected for a supplier request.{' '}
          <button
            type="button"
            onClick={clearCart}
            className="underline underline-offset-2 hover:text-ink"
          >
            Clear selection
          </button>
        </p>
      )}

      <StockActionModal
        mode={action.mode}
        item={action.item}
        onClose={() => setAction({ mode: null, item: null })}
        onSuccess={reload}
      />

      <StockHistoryModal
        open={Boolean(historyItem)}
        item={historyItem}
        onClose={() => setHistoryItem(null)}
      />

      <SupplierRequestModal
        open={cartOpen}
        rows={cartRows}
        quantities={cartQuantities}
        onQuantityChange={(itemId, value) =>
          setCartQuantities((current) => ({ ...current, [itemId]: value }))
        }
        onRemove={removeFromCart}
        onClose={() => setCartOpen(false)}
        onSuccess={() => {
          clearCart()
          reload()
        }}
      />
    </>
  )
}
