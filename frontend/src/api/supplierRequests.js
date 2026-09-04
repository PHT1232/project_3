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
 * Creating an order does NOT move stock. The order is recorded "PendingArrival"; the balance only
 * rises when a Business Manager confirms the goods arrived — confirmSupplierRequestArrival() below.
 */

export const SUPPLIER_ORDER_STATUS = {
  PENDING_ARRIVAL: 'PendingArrival',
  RECEIVED: 'Received',
}

export async function createSupplierRequests(items) {
  return (await client.post('/supplier-requests', { items })).data
}

export async function getSupplierRequests({ page = 1, pageSize = 20 } = {}) {
  return (await client.get('/supplier-requests', { params: { page, pageSize } })).data
}

/**
 * "Goods Arrived" — Business Manager only (rank >= 3); a Manager gets 403.
 * Posts the stock receipt for every line, exactly once. Confirming an order that is already
 * Received returns 409, so a double-click cannot inflate the balance.
 */
export async function confirmSupplierRequestArrival(supplierRequestId) {
  return (await client.post(`/supplier-requests/${supplierRequestId}/confirm-arrival`)).data
}
