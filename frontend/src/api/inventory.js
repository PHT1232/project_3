import client from './client.js'

export const INVENTORY_STATUS = {
  OK: 'OK',
  WATCH: 'WATCH',
  REORDER_NOW: 'REORDER_NOW',
}

/**
 * Inventory data access. Components import from here and never touch the data source directly.
 * All endpoints are Manager+ (Plan §4.2, m2 plan §5.1).
 */

export async function getInventory() {
  const { data } = await client.get('/inventory', { params: { pageSize: 200 } })
  return { items: data.page.items, summary: data.summary }
}

export async function getLowStock() {
  return (await client.get('/inventory/low-stock')).data
}

export async function adjustStock(itemId, { changeQuantity, reason, rowVersion }) {
  return (await client.post(`/inventory/${itemId}/adjust`, { changeQuantity, reason, rowVersion })).data
}

export async function receiveGoods(itemId, { quantity, supplierId, reference, rowVersion }) {
  return (await client.post(`/inventory/${itemId}/receive`, { quantity, supplierId, reference, rowVersion })).data
}

export async function getTransactionHistory(itemId) {
  return (await client.get(`/inventory/${itemId}/transactions`)).data
}
