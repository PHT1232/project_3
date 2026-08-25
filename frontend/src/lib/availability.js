/**
 * Catalogue availability band.
 *
 * DOCUMENTED RULE — the "low stock" threshold is `QuantityAvailable <= ReorderLevel`, per the
 * Plan §4.2 (`GET /api/v1/inventory/low-stock` — "Items at or below reorder level") and the
 * M2 acceptance criterion "`/low-stock` correctly identifies items at or below `ReorderLevel`".
 * Zero stock is shown as "Out of Stock" on the approved Catalogue wireframe.
 *
 * This is the only stock rule computed on the client. The Inventory page's three-way
 * OK / WATCH / REORDER NOW status is NOT derived here — see `src/api/inventory.js`.
 */
export const AVAILABILITY = {
  IN_STOCK: 'IN_STOCK',
  LOW_STOCK: 'LOW_STOCK',
  OUT_OF_STOCK: 'OUT_OF_STOCK',
}

export function getAvailability(item) {
  if (item.quantityAvailable <= 0) return AVAILABILITY.OUT_OF_STOCK
  if (item.quantityAvailable <= item.reorderLevel) return AVAILABILITY.LOW_STOCK
  return AVAILABILITY.IN_STOCK
}

/** Label shown on the catalogue card badge, e.g. "In Stock (124)". */
export function getAvailabilityLabel(item) {
  const availability = getAvailability(item)
  if (availability === AVAILABILITY.OUT_OF_STOCK) return 'Out of Stock'
  const qty = item.quantityAvailable > 200 ? '200+' : item.quantityAvailable
  return availability === AVAILABILITY.LOW_STOCK ? `Low Stock (${qty})` : `In Stock (${qty})`
}
