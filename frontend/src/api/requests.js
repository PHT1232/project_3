import client from './client.js'

/**
 * Stationery Request API client (Plan §3.4–§4.2).
 * Backed by WebApi/Controllers/RequestsController.cs and ApprovalController.cs.
 */

// --- Requestor / Visibility endpoints ---

/**
 * Get visible requests: the caller's own, any pending their approval, and (for a Manager /
 * Business Manager) every request raised inside their reporting sub-tree. The Managing
 * Director sees all. Scoping is enforced server-side.
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
 * Create a new stationery request (status: Pending).
 * @param {Object} payload - { items: [{ itemId, quantity }], requiredByDate?: string }
 */
export async function createRequest({ items, requiredByDate }) {
  return (await client.post('/requests', { items, requiredByDate: requiredByDate || null })).data
}

/**
 * Submit an unsubmitted Pending request for approval.
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
 * Delete an unsubmitted Pending request.
 */
export async function deletePendingRequest(requestId) {
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
 * Get requests pending the current user's approval.
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
