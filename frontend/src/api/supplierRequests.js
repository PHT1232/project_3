import client from './client.js'

/**
 * Supplier replenishment orders raised from the inventory cart. Manager+ only.
 *
 * POST /api/v1/supplier-requests
 *   body : { items: [{ itemId, quantity, supplierId }] }
 *          supplierId is only consulted for items with no preferred supplier — when the item has
 *          one, the server uses that and ignores whatever is sent here.
 *   201  : SupplierRequestDto[] — one entry per distinct supplier, already grouped.
 *   400  : ProblemDetails with `errors.items[]` when any line fails validation. The whole
 *          submission is rejected; nothing is created.
 *
 * Creating an order does NOT move stock — that happens later via receiveGoods() in inventory.js.
 */

export async function createSupplierRequests(items) {
  return (await client.post('/supplier-requests', { items })).data
}

export async function getSupplierRequests({ page = 1, pageSize = 20 } = {}) {
  return (await client.get('/supplier-requests', { params: { page, pageSize } })).data
}
