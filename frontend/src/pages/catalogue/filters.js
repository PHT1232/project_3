import { getAvailability, AVAILABILITY } from '../../lib/availability.js'

/** Slider ceiling. At the cap the filter is "$100+", i.e. no upper bound. */
export const MAX_COST_CAP = 100

export const DEFAULT_FILTERS = {
  categoryIds: [],
  supplierId: '',
  availability: 'ALL',
  maxUnitCost: MAX_COST_CAP,
}

export function isDefaultFilters(filters) {
  return (
    filters.categoryIds.length === 0 &&
    !filters.supplierId &&
    filters.availability === 'ALL' &&
    filters.maxUnitCost === MAX_COST_CAP
  )
}

/**
 * Pure filter + search over the catalogue. Kept out of the component so it is trivially
 * testable and so the page body stays declarative.
 */
export function applyCatalogueFilters(items, filters, searchTerm) {
  const term = searchTerm.trim().toLowerCase()

  return items.filter((item) => {
    if (filters.categoryIds.length > 0 && !filters.categoryIds.includes(item.categoryId)) {
      return false
    }
    if (filters.supplierId && item.supplierId !== Number(filters.supplierId)) {
      return false
    }
    if (filters.availability === 'IN_STOCK' && getAvailability(item) === AVAILABILITY.OUT_OF_STOCK) {
      return false
    }
    if (filters.maxUnitCost < MAX_COST_CAP && item.unitCost > filters.maxUnitCost) {
      return false
    }
    if (term && !item.itemName.toLowerCase().includes(term)) {
      return false
    }
    return true
  })
}

/** Chips shown in the "Active Filters" row. */
export function describeActiveFilters(filters, categories, suppliers) {
  const chips = []

  for (const categoryId of filters.categoryIds) {
    const category = categories.find((c) => c.categoryId === categoryId)
    if (category) {
      chips.push({
        key: `category-${categoryId}`,
        label: category.name,
        clear: (current) => ({
          ...current,
          categoryIds: current.categoryIds.filter((id) => id !== categoryId),
        }),
      })
    }
  }

  if (filters.supplierId) {
    const supplier = suppliers.find((s) => s.supplierId === Number(filters.supplierId))
    if (supplier) {
      chips.push({
        key: 'supplier',
        label: supplier.name,
        clear: (current) => ({ ...current, supplierId: '' }),
      })
    }
  }

  if (filters.availability === 'IN_STOCK') {
    chips.push({
      key: 'availability',
      label: 'In Stock Only',
      clear: (current) => ({ ...current, availability: 'ALL' }),
    })
  }

  if (filters.maxUnitCost < MAX_COST_CAP) {
    chips.push({
      key: 'cost',
      label: `Unit Cost < $${filters.maxUnitCost}`,
      clear: (current) => ({ ...current, maxUnitCost: MAX_COST_CAP }),
    })
  }

  return chips
}
