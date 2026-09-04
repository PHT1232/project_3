import client from './client.js'

/**
 * Stationery Request API client (Plan §3.4–§4.2).
 * Backed by WebApi/Controllers/RequestsController.cs and ApprovalController.cs.
 */

// --- Requestor / Visibility endpoints ---

/**
 * Get visible requests (caller's own + subordinates if approver, or all if Manager+).
 */
export async function getRequests({ page = 1, pageSize = 20, status } = {}) {
  return (await client.get('/requests', { params: { page, pageSize, status: status || undefined } })).data
}

/**
 * Get current user's own requests.
 */
export async function getMyRequests({ page = 1, pageSize = 20, status } = {}) {
  return (await client.get('/requests/mine', { params: { page, pageSize, status: status || undefined } })).data
}

/**
 * Get single request details by ID.
 */
export async function getRequest(requestId) {
  return (await client.get(`/requests/${requestId}`)).data
}

/**
 * Create a new stationery request. It is saved as a Draft — invisible to the approver until
 * submitRequest() moves it to Pending (Plan §3.6).
 * @param {Object} payload - { items: [{ itemId, quantity }], requiredByDate?: string }
 */
export async function createRequest({ items, requiredByDate }) {
  return (await client.post('/requests', { items, requiredByDate: requiredByDate || null })).data
}

/**
 * Submit a Draft request for approval (Draft → Pending). Notifies the approver.
 */
export async function submitRequest(requestId, rowVersion) {
  return (await client.post(`/requests/${requestId}/submit`, { requestId, rowVersion })).data
}

/**
 * Withdraw a Pending request (requestor only).
 */
export async function withdrawRequest(requestId, rowVersion) {
  return (await client.post(`/requests/${requestId}/withdraw`, { requestId, rowVersion })).data
}

/**
 * Request cancellation for an already Approved / PartiallyApproved request.
 */
export async function requestCancellation(requestId, rowVersion, reason) {
  return (
    await client.post(`/requests/${requestId}/request-cancellation`, {
      requestId,
      rowVersion,
      reason: reason || null,
    })
  ).data
}

/**
 * Delete a Draft request. Draft is the only deletable state — a submitted request is never
 * deleted, only transitioned (Plan §3.6).
 */
export async function deleteDraftRequest(requestId) {
  return (await client.delete(`/requests/${requestId}`)).data
}

/**
 * Status counts for dashboard summary.
 */
export async function getRequestSummary() {
  return (await client.get('/requests/dashboard-summary')).data
}

// --- Approver endpoints ---

/**
 * Get requests awaiting the current user's decision: Pending (approve / reject) and
 * CancellationPending (approve / refuse the cancellation).
 */
export async function getPendingApprovals({ page = 1, pageSize = 20 } = {}) {
  return (await client.get('/approvals/pending', { params: { page, pageSize } })).data
}

/**
 * Approve, reject, or partially approve a request.
 */
export async function approveRequest(requestId, { rowVersion, lineDecisions, comment }) {
  return (
    await client.post(`/approvals/${requestId}/approve`, {
      requestId,
      rowVersion,
      lineDecisions,
      comment,
    })
  ).data
}

/**
 * Approver decides on a cancellation request.
 */
export async function approveCancellation(requestId, { rowVersion, approved, reason }) {
  return (
    await client.post(`/approvals/${requestId}/cancel-approval`, {
      requestId,
      rowVersion,
      approved,
      reason: reason || null,
    })
  ).data
}
