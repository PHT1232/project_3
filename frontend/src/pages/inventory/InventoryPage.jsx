import { useMemo, useState } from 'react'
import { SlidersHorizontal, PackagePlus, ShoppingCart } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import StatCard from '../../components/ui/StatCard.jsx'
import { ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import { Skeleton, SkeletonStatCards, SkeletonTable } from '../../components/ui/Skeleton.jsx'
import useAsync from '../../hooks/useAsync.js'
import useSortableTable from '../../hooks/useSortableTable.js'
import { getInventory, INVENTORY_STATUS } from '../../api/inventory.js'
import { formatCurrency, formatNumber } from '../../lib/format.js'

import InventoryToolbar from './components/InventoryToolbar.jsx'
import InventoryTable from './components/InventoryTable.jsx'
import Pagination from '../../components/ui/Pagination.jsx'
import usePagination from '../../hooks/usePagination.js'
import StockActionModal from './components/StockActionModal.jsx'
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

  const { page, setPage, totalPages, total, pageRows } = usePagination(visibleRows)

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

  return (
    <>
      <PageHeader
        title="Inventory / Stock Ledger"
        description="Real-time overview of all stationery items in stock."
        actions={
          <>
            <Button
              variant="secondary"
              onClick={() => setAction({ mode: 'adjust', item: visibleRows[0] ?? null })}
              disabled={visibleRows.length === 0}
            >
              <SlidersHorizontal className="h-4 w-4" aria-hidden="true" />
              Adjust Stock
            </Button>
            <Button
              variant="secondary"
              onClick={() => setAction({ mode: 'receive', item: visibleRows[0] ?? null })}
              disabled={visibleRows.length === 0}
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

      {loading && (
        <>
          <SkeletonStatCards
            label="Loading inventory summary…"
            count={3}
            grid="grid-cols-1 sm:grid-cols-3"
            className="mb-6"
          />
          {/* Toolbar placeholder — mirrors InventoryToolbar's search + status filter row. */}
          <Card className="mb-4 flex flex-col gap-3 p-3 lg:flex-row lg:items-center">
            <Skeleton className="h-10 w-full lg:max-w-sm lg:flex-1" />
            <Skeleton className="h-10 w-44 lg:ml-auto" />
          </Card>
        </>
      )}

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
        {loading && (
          <SkeletonTable
            label="Loading inventory…"
            rows={8}
            columns={[
              { width: 1, bar: 'w-4' },
              5,
              2,
              { width: 2, align: 'right' },
              { width: 2, align: 'right' },
              { width: 2, height: 'h-6' },
              { width: 1, bar: 'w-8', height: 'h-8' },
            ]}
          />
        )}

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
          <>
            <InventoryTable
              rows={pageRows}
              selectedIds={selectedIds}
              onToggle={toggleRow}
              onToggleAll={toggleAll}
              onAdjust={(item) => setAction({ mode: 'adjust', item })}
              onReceive={(item) => setAction({ mode: 'receive', item })}
              headerProps={headerProps}
            />
            <Pagination
              page={page}
              totalPages={totalPages}
              total={total}
              onPageChange={setPage}
            />
          </>
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
