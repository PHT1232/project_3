import { MOCK_INVENTORY, MOCK_INVENTORY_SUMMARY } from './mock/inventory.mock.js'

export { INVENTORY_STATUS } from './mock/inventory.mock.js'

/**
 * Inventory data access. Components import from here and never touch the data source directly.
 *
 * ---------------------------------------------------------------------------
 * EXPECTED BACKEND CONTRACT (Plan §4.2 — M3's work, not implemented yet)
 *
 *   GET /api/v1/inventory                      auth: Manager+
 *     200 : { items: InventoryRowDto[], summary: SummaryDto, page, pageSize, totalCount }
 *
 *   GET /api/v1/inventory/low-stock            auth: Manager+
 *     200 : InventoryRowDto[]   (items at or below ReorderLevel)
 *
 *   POST /api/v1/inventory/{itemId}/adjust     auth: Manager+
 *     body : { changeQuantity: int (may be negative), reason: string (REQUIRED) }
 *     note : writes a StockTransactions ledger row in the same transaction as the balance
 *            change (Plan §3.5 — the ledger is append-only and is the source of truth).
 *     200 : InventoryRowDto   ·  400 reason missing  ·  409 stale RowVersion
 *
 *   POST /api/v1/inventory/{itemId}/receive    auth: Manager+
 *     body : { quantity: int > 0, supplierId?: int, reference?: string }
 *     200 : InventoryRowDto   ·  400 invalid quantity
 *
 *   GET /api/v1/inventory/{itemId}/transactions  auth: Manager+
 *     200 : ledger history for the item
 *
 *   InventoryRowDto { itemId, itemName, sku, quantityAvailable, reorderLevel, unitCost,
 *                     status: 'OK' | 'WATCH' | 'REORDER_NOW' }
 *   SummaryDto      { totalItems, lowStockAlerts, totalValue }
 *
 *   `status` is server-derived — see the note in ./mock/inventory.mock.js.
 *
 * TO GO LIVE: replace each function body with the `client` call shown and delete
 * `./mock/inventory.mock.js`. No component changes are required.
 * ---------------------------------------------------------------------------
 */

// import client from './client.js'

export async function getInventory() {
  // return (await client.get('/inventory')).data
  return { items: MOCK_INVENTORY, summary: MOCK_INVENTORY_SUMMARY }
}

/**
 * Stock movements are not available until the endpoints above exist. These reject with an
 * explicit message rather than pretending to succeed, so the UI shows a truthful failure
 * instead of fake confirmation. This is the only place that changes when the API lands.
 */
export async function adjustStock() {
  // return (await client.post(`/inventory/${itemId}/adjust`, { changeQuantity, reason })).data
  throw new Error(
    'Stock adjustment is not available yet — POST /api/v1/inventory/{itemId}/adjust has not been implemented.',
  )
}

export async function receiveGoods() {
  // return (await client.post(`/inventory/${itemId}/receive`, { quantity, supplierId })).data
  throw new Error(
    'Goods receipt is not available yet — POST /api/v1/inventory/{itemId}/receive has not been implemented.',
  )
}
