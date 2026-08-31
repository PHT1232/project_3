import client from './client.js'

/**
 * Request approval workflow (Plan §3.6/§4.2). Backed by WebApi/Controllers/ApprovalController.cs.
 *
 * Only the approver-facing endpoints exist on the backend today — there is no
 * `RequestsController` yet, so requestor actions (create/submit/withdraw/list-my-requests) have
 * no endpoint to call. `pages/NewRequest.jsx` and `pages/MyRequests.jsx` stay placeholders until
 * that lands; see docs/development/request-approval-frontend-implementation-plan.md.
 */

export async function getPendingApprovals({ page = 1, pageSize = 20 } = {}) {
  return (await client.get('/approvals/pending', { params: { page, pageSize } })).data
}

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
