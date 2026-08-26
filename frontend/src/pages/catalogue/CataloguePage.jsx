import { useMemo, useState } from 'react'
import { X, SlidersHorizontal } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Button from '../../components/ui/Button.jsx'
import SearchInput from '../../components/ui/SearchInput.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { getItems, getCategories } from '../../api/catalogue.js'

import CatalogueFilters from './components/CatalogueFilters.jsx'
import ItemCard from './components/ItemCard.jsx'
import {
  DEFAULT_FILTERS,
  isDefaultFilters,
  applyCatalogueFilters,
  describeActiveFilters,
} from './filters.js'

export default function CataloguePage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [filters, setFilters] = useState(DEFAULT_FILTERS)
  const [panelOpen, setPanelOpen] = useState(true)

  const { data, error, loading, reload } = useAsync(
    () => Promise.all([getItems(), getCategories()]).then(([items, categories]) => ({ items, categories })),
    [],
  )

  const items = data?.items ?? []
  const categories = data?.categories ?? []

  const visibleItems = useMemo(
    () => applyCatalogueFilters(items, filters, searchTerm),
    [items, filters, searchTerm],
  )
  const activeChips = useMemo(
    () => describeActiveFilters(filters, categories),
    [filters, categories],
  )

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
            <CatalogueFilters categories={categories} value={filters} onChange={setFilters} />
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
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {visibleItems.map((item) => (
                <ItemCard key={item.itemId} item={item} />
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  )
}
