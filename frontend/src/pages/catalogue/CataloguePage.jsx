import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { X, SlidersHorizontal } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Button from '../../components/ui/Button.jsx'
import SearchInput from '../../components/ui/SearchInput.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { getItems, getCategories } from '../../api/catalogue.js'

import CatalogueFilters from './components/CatalogueFilters.jsx'
import ItemCard from './components/ItemCard.jsx'
import CatalogueSelectionBar from './components/CatalogueSelectionBar.jsx'
import {
  DEFAULT_FILTERS,
  isDefaultFilters,
  applyCatalogueFilters,
  describeActiveFilters,
} from './filters.js'

const PAGE_SIZE = 15

export default function CataloguePage() {
  const navigate = useNavigate()
  const [searchTerm, setSearchTerm] = useState('')
  const [filters, setFilters] = useState(DEFAULT_FILTERS)
  const [panelOpen, setPanelOpen] = useState(true)
  const [page, setPage] = useState(1)
  // Items picked from the grid, held here until the user proceeds. Plain useState — the selection
  // is scoped to this page and handed off on navigation, so it needs no global store (Plan §2.4).
  const [selectedItems, setSelectedItems] = useState([])

  const { data, error, loading, reload } = useAsync(
    () =>
      Promise.all([getItems(), getCategories()]).then(([items, categories]) => ({
        items,
        categories,
        suppliers: [...new Map(items.filter((item) => item.supplierId && item.supplierName).map((item) => [
          item.supplierId,
          { supplierId: item.supplierId, name: item.supplierName },
        ])).values()],
      })),
    [],
  )

  const items = data?.items ?? []
  const categories = data?.categories ?? []
  const suppliers = data?.suppliers ?? []

  const visibleItems = useMemo(
    () => applyCatalogueFilters(items, filters, searchTerm),
    [items, filters, searchTerm],
  )
  const activeChips = useMemo(
    () => describeActiveFilters(filters, categories, suppliers),
    [filters, categories, suppliers],
  )

  // Any change to filters or search reshuffles which items match, so the current page number
  // may no longer be valid (or may now be showing a stale slice) — reset to page 1.
  useEffect(() => {
    setPage(1)
  }, [filters, searchTerm])

  const totalPages = Math.max(1, Math.ceil(visibleItems.length / PAGE_SIZE))
  const pagedItems = useMemo(
    () => visibleItems.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [visibleItems, page],
  )

  const selectedIds = useMemo(
    () => new Set(selectedItems.map((item) => item.itemId)),
    [selectedItems],
  )

  function addItem(item) {
    setSelectedItems((current) =>
      current.some((selected) => selected.itemId === item.itemId) ? current : [...current, item],
    )
  }

  /** Hands the selection to the New Request page, which owns quantities and submission. */
  function proceedToRequest() {
    navigate('/new-request', { state: { items: selectedItems } })
  }

  function clearAll() {
    setFilters(DEFAULT_FILTERS)
    setSearchTerm('')
  }

  return (
    <>
      <PageHeader
        title="Stationery Catalogue"
        description="Browse and request available items for your department."
        actions={
          <>
            <SearchInput
              value={searchTerm}
              onChange={setSearchTerm}
              placeholder="Search catalogue..."
              label="Search catalogue"
              className="w-full sm:w-64"
            />
            <Button
              variant="secondary"
              onClick={() => setPanelOpen((open) => !open)}
              aria-expanded={panelOpen}
            >
              <SlidersHorizontal className="h-4 w-4" aria-hidden="true" />
              Filters
            </Button>
          </>
        }
      />

      <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
        {panelOpen && (
          <div className="w-full shrink-0 lg:w-64">
            <CatalogueFilters categories={categories} suppliers={suppliers} value={filters} onChange={setFilters} />
          </div>
        )}

        <div className="min-w-0 flex-1">
          {activeChips.length > 0 && (
            <div className="mb-4 flex flex-wrap items-center gap-2">
              <span className="text-sm text-ink-muted">Active Filters:</span>
              {activeChips.map((chip) => (
                <span
                  key={chip.key}
                  className="inline-flex items-center gap-1.5 rounded-md bg-surface-muted px-2.5 py-1 text-xs font-medium text-ink"
                >
                  {chip.label}
                  <button
                    type="button"
                    onClick={() => setFilters((current) => chip.clear(current))}
                    aria-label={`Remove filter ${chip.label}`}
                    className="rounded text-ink-muted hover:text-ink"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                </span>
              ))}
              <button
                type="button"
                onClick={clearAll}
                className="text-sm font-semibold text-brand-700 underline underline-offset-2"
              >
                Clear All
              </button>
            </div>
          )}

          {loading && <LoadingState label="Loading catalogue…" />}

          {!loading && error && <ErrorState error={error} onRetry={reload} />}

          {!loading && !error && visibleItems.length === 0 && (
            <EmptyState
              title={items.length === 0 ? 'No items in the catalogue' : 'No items match your filters'}
              description={
                items.length === 0
                  ? 'Once stationery items are added they will appear here.'
                  : 'Try widening the unit cost range or selecting more categories.'
              }
              action={
                items.length > 0 &&
                !(isDefaultFilters(filters) && !searchTerm) && (
                  <Button variant="secondary" size="sm" onClick={clearAll}>
                    Clear all filters
                  </Button>
                )
              }
            />
          )}

          {!loading && !error && visibleItems.length > 0 && (
            <>
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
                {pagedItems.map((item) => (
                  <ItemCard
                    key={item.itemId}
                    item={item}
                    onAdd={addItem}
                    added={selectedIds.has(item.itemId)}
                  />
                ))}
              </div>

              <div className="mt-4 flex items-center justify-between border-t border-surface-border px-4 py-3 text-sm text-ink-muted">
                <span>
                  Page {page} of {totalPages} · {visibleItems.length} item
                  {visibleItems.length === 1 ? '' : 's'}
                </span>
                <div className="flex gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => p - 1)}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Next
                  </Button>
                </div>
              </div>
            </>
          )}

          {/* Outside the results conditional: a selection must survive filters that hide it. */}
          <CatalogueSelectionBar
            items={selectedItems}
            onClear={() => setSelectedItems([])}
            onProceed={proceedToRequest}
          />
        </div>
      </div>
    </>
  )
}
