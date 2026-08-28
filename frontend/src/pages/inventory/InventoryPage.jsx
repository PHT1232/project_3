import { useMemo, useState } from 'react'
import { SlidersHorizontal, PackagePlus } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import StatCard from '../../components/ui/StatCard.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { getInventory } from '../../api/inventory.js'
import { formatCurrency, formatNumber } from '../../lib/format.js'

import InventoryToolbar from './components/InventoryToolbar.jsx'
import InventoryTable from './components/InventoryTable.jsx'
import StockActionModal from './components/StockActionModal.jsx'

function sortRows(rows, sort) {
  const sorted = [...rows]
  switch (sort) {
    case 'NAME_DESC':
      return sorted.sort((a, b) => b.itemName.localeCompare(a.itemName))
    case 'STOCK_ASC':
      return sorted.sort((a, b) => a.quantityAvailable - b.quantityAvailable)
    case 'STOCK_DESC':
      return sorted.sort((a, b) => b.quantityAvailable - a.quantityAvailable)
    default:
      return sorted.sort((a, b) => a.itemName.localeCompare(b.itemName))
  }
}

export default function InventoryPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [status, setStatus] = useState('ALL')
  const [sort, setSort] = useState('NAME_ASC')
  const [selectedIds, setSelectedIds] = useState([])
  const [action, setAction] = useState({ mode: null, item: null })

  const { data, error, loading, reload } = useAsync(() => getInventory(), [])

  const rows = data?.items ?? []
  const summary = data?.summary

  const visibleRows = useMemo(() => {
    const term = searchTerm.trim().toLowerCase()
    const filtered = rows.filter((row) => {
      if (status !== 'ALL' && row.status !== status) return false
      if (!term) return true
      return (
        row.itemName.toLowerCase().includes(term) || (row.sku ?? '').toLowerCase().includes(term)
      )
    })
    return sortRows(filtered, sort)
  }, [rows, searchTerm, status, sort])

  function toggleRow(itemId) {
    setSelectedIds((current) =>
      current.includes(itemId) ? current.filter((id) => id !== itemId) : [...current, itemId],
    )
  }

  function toggleAll() {
    const visibleIds = visibleRows.map((row) => row.itemId)
    const allSelected = visibleIds.every((id) => selectedIds.includes(id))
    setSelectedIds(allSelected ? [] : visibleIds)
  }

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
              onClick={() => setAction({ mode: 'receive', item: visibleRows[0] ?? null })}
              disabled={visibleRows.length === 0}
            >
              <PackagePlus className="h-4 w-4" aria-hidden="true" />
              Receive Goods
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
          sort={sort}
          onSort={setSort}
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
          />
        )}
      </Card>

      {selectedIds.length > 0 && (
        <p className="mt-3 text-sm text-ink-muted">
          {selectedIds.length} item{selectedIds.length === 1 ? '' : 's'} selected
        </p>
      )}

      <StockActionModal
        mode={action.mode}
        item={action.item}
        onClose={() => setAction({ mode: null, item: null })}
        onSuccess={reload}
      />
    </>
  )
}
