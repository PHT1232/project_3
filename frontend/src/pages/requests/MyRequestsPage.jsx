import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PlusCircle, Eye, Send, Undo2, XCircle, Trash2, AlertCircle, CheckCircle2, SlidersHorizontal } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import { ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import { SkeletonTable } from '../../components/ui/Skeleton.jsx'
import useAsync from '../../hooks/useAsync.js'
import { getMyRequests, submitRequest, withdrawRequest, requestCancellation, deleteDraftRequest } from '../../api/requests.js'
import { formatCurrency, formatDate } from '../../lib/format.js'
import RequestStatusBadge from './components/RequestStatusBadge.jsx'
import RequestDetailModal from './components/RequestDetailModal.jsx'
import CancellationModal from './components/CancellationModal.jsx'

const PAGE_SIZE = 15

const STATUS_OPTIONS = [
  { value: '', label: 'All Statuses' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Approved', label: 'Approved' },
  { value: 'PartiallyApproved', label: 'Partially Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Withdrawn', label: 'Withdrawn' },
  { value: 'CancellationPending', label: 'Cancellation Pending' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Fulfilled', label: 'Fulfilled' },
]

export default function MyRequestsPage() {
  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] = useState('')
  const [selectedRequest, setSelectedRequest] = useState(null)
  const [cancellationTarget, setCancellationTarget] = useState(null)
  const [isActioning, setIsActioning] = useState(false)
  const [actionError, setActionError] = useState(null)
  const [actionSuccess, setActionSuccess] = useState(null)

  const { data, error, loading, reload } = useAsync(
    () => getMyRequests({ page, pageSize: PAGE_SIZE, status: statusFilter }),
    [page, statusFilter],
  )

  const requests = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  function clearMessages() {
    setActionError(null)
    setActionSuccess(null)
  }

  async function handleSubmitDraft(request) {
    if (!request) return
    setIsActioning(true)
    clearMessages()
    try {
      await submitRequest(request.requestId, request.rowVersion)
      setActionSuccess(`Request #${request.requestId} submitted for approval successfully.`)
      setSelectedRequest(null)
      reload()
    } catch (err) {
      const msg = err.response?.data?.detail ?? err.response?.data?.error ?? err.message ?? 'Failed to submit request.'
      setActionError(msg)
    } finally {
      setIsActioning(false)
    }
  }

  async function handleWithdraw(request) {
    if (!request) return
    if (!window.confirm(`Are you sure you want to withdraw request #${request.requestId}?`)) return

    setIsActioning(true)
    clearMessages()
    try {
      await withdrawRequest(request.requestId, request.rowVersion)
      setActionSuccess(`Request #${request.requestId} withdrawn.`)
      setSelectedRequest(null)
      reload()
    } catch (err) {
      const msg = err.response?.data?.detail ?? err.response?.data?.error ?? err.message ?? 'Failed to withdraw request.'
      setActionError(msg)
    } finally {
      setIsActioning(false)
    }
  }

  async function handleDeleteDraft(request) {
    if (!request) return
    if (!window.confirm(`Are you sure you want to delete draft request #${request.requestId}?`)) return

    setIsActioning(true)
    clearMessages()
    try {
      await deleteDraftRequest(request.requestId)
      setActionSuccess(`Draft request #${request.requestId} deleted.`)
      setSelectedRequest(null)
      reload()
    } catch (err) {
      const msg = err.response?.data?.detail ?? err.response?.data?.error ?? err.message ?? 'Failed to delete request.'
      setActionError(msg)
    } finally {
      setIsActioning(false)
    }
  }

  async function handleConfirmCancellation(request, reason) {
    if (!request) return
    setIsActioning(true)
    clearMessages()
    try {
      await requestCancellation(request.requestId, request.rowVersion, reason)
      setActionSuccess(`Cancellation requested for #${request.requestId}. Awaiting approver confirmation.`)
      setCancellationTarget(null)
      setSelectedRequest(null)
      reload()
    } catch (err) {
      const msg = err.response?.data?.detail ?? err.response?.data?.error ?? err.message ?? 'Failed to request cancellation.'
      setActionError(msg)
    } finally {
      setIsActioning(false)
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Requests"
        description="View and manage your stationery requests, track approvals, and view requisition history."
        actions={
          <Link to="/new-request">
            <Button variant="primary">
              <PlusCircle className="h-4 w-4" aria-hidden="true" />
              New Request
            </Button>
          </Link>
        }
      />

      {/* Action Messages */}
      {actionError && (
        <div className="flex items-start gap-3 rounded-lg border border-status-dangerBorder bg-status-dangerBg p-4 text-sm text-status-danger">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
          <div className="flex-1 font-medium">{actionError}</div>
        </div>
      )}

      {actionSuccess && (
        <div className="flex items-start gap-3 rounded-lg border border-status-successBorder bg-status-successBg p-4 text-sm text-status-success">
          <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" aria-hidden="true" />
          <div className="flex-1 font-medium">{actionSuccess}</div>
        </div>
      )}

      <Card>
        {/* Filters Toolbar */}
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3 px-4 pt-4">
          <div className="flex items-center gap-2">
            <SlidersHorizontal className="h-4 w-4 text-ink-muted" aria-hidden="true" />
            <label htmlFor="status-filter" className="text-xs font-semibold uppercase tracking-wider text-ink-muted">
              Filter by Status:
            </label>
            <select
              id="status-filter"
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value)
                setPage(1)
              }}
              className="h-9 rounded-md border border-surface-border bg-surface-card px-2.5 text-sm text-ink focus:border-brand-500 focus:outline-none"
            >
              {STATUS_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div className="text-xs text-ink-muted">
            Total: <span className="font-semibold text-ink">{totalCount}</span> requests
          </div>
        </div>

        {/* Content States */}
        {loading && (
          <SkeletonTable
            label="Loading your stationery requests…"
            rows={8}
            columns={[
              4,
              5,
              4,
              4,
              2,
              { width: 3, align: 'right' },
              { width: 3, height: 'h-6' },
              { width: 5, align: 'right', height: 'h-8' },
            ]}
          />
        )}
        {!loading && error && <ErrorState error={error} onRetry={reload} />}
        {!loading && !error && requests.length === 0 && (
          <EmptyState
            title={statusFilter ? `No requests in "${statusFilter}" status` : 'No stationery requests yet'}
            description={
              statusFilter
                ? 'Try selecting a different status filter or create a new request.'
                : 'You have not submitted any stationery requests. Create your first request to get started.'
            }
            action={
              <Link to="/new-request">
                <Button variant="primary" size="sm">
                  <PlusCircle className="h-4 w-4" aria-hidden="true" />
                  Create Request
                </Button>
              </Link>
            }
          />
        )}

        {/* Requests Table */}
        {!loading && !error && requests.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-surface-border text-xs uppercase tracking-wider text-ink-muted">
                    <th className="px-4 py-3 font-semibold">Request #</th>
                    <th className="px-4 py-3 font-semibold">Date Created</th>
                    <th className="px-4 py-3 font-semibold">Required By</th>
                    <th className="px-4 py-3 font-semibold">Approver</th>
                    <th className="px-4 py-3 font-semibold text-center">Items</th>
                    <th className="px-4 py-3 font-semibold text-right">Est. Cost</th>
                    <th className="px-4 py-3 font-semibold">Status</th>
                    <th className="px-4 py-3 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-surface-border">
                  {requests.map((req) => {
                    // Draft = saved, not yet sent (submit / delete). Pending = sent, awaiting the
                    // approver (withdraw). The server owns this distinction now — it used to be
                    // inferred here from status history, which the server never checked.
                    const isDraft = req.status === 'Draft'
                    const isPending = req.status === 'Pending'
                    const isApprovedOrPartial = req.status === 'Approved' || req.status === 'PartiallyApproved'

                    return (
                      <tr key={req.requestId} className="hover:bg-surface-muted/30">
                        <td className="px-4 py-3 font-mono font-medium text-ink">
                          #{req.requestId}
                        </td>
                        <td className="px-4 py-3 text-ink-muted">
                          {formatDate(req.createdAtUtc)}
                        </td>
                        <td className="px-4 py-3 text-ink-muted">
                          {req.requiredByDate ? formatDate(req.requiredByDate) : '—'}
                        </td>
                        <td className="px-4 py-3 text-ink">
                          {req.approverName ?? (req.approverEmployeeNumber ? `#${req.approverEmployeeNumber}` : '—')}
                        </td>
                        <td className="px-4 py-3 text-center text-ink-muted">
                          {req.items?.length ?? 0}
                        </td>
                        <td className="px-4 py-3 text-right font-mono font-medium text-ink">
                          {formatCurrency(req.totalEstimatedCost)}
                        </td>
                        <td className="px-4 py-3">
                          <RequestStatusBadge status={req.status} />
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            <Button
                              size="sm"
                              variant="secondary"
                              onClick={() => setSelectedRequest(req)}
                              aria-label={`View details for request #${req.requestId}`}
                            >
                              <Eye className="h-4 w-4" aria-hidden="true" />
                              View
                            </Button>

                            {isDraft && (
                              <Button
                                size="sm"
                                variant="primary"
                                disabled={isActioning}
                                onClick={() => handleSubmitDraft(req)}
                                aria-label={`Submit request #${req.requestId}`}
                              >
                                <Send className="h-4 w-4" aria-hidden="true" />
                                Submit
                              </Button>
                            )}

                            {isPending && (
                              <Button
                                size="sm"
                                variant="secondary"
                                disabled={isActioning}
                                onClick={() => handleWithdraw(req)}
                                aria-label={`Withdraw request #${req.requestId}`}
                              >
                                <Undo2 className="h-4 w-4" aria-hidden="true" />
                                Withdraw
                              </Button>
                            )}

                            {isApprovedOrPartial && (
                              <Button
                                size="sm"
                                variant="secondary"
                                disabled={isActioning}
                                onClick={() => setCancellationTarget(req)}
                                aria-label={`Request cancellation for #${req.requestId}`}
                              >
                                <XCircle className="h-4 w-4" aria-hidden="true" />
                                Cancel
                              </Button>
                            )}

                            {isDraft && (
                              <button
                                type="button"
                                disabled={isActioning}
                                onClick={() => handleDeleteDraft(req)}
                                aria-label={`Delete draft request #${req.requestId}`}
                                className="rounded p-1 text-ink-muted hover:bg-surface-muted hover:text-status-danger"
                              >
                                <Trash2 className="h-4 w-4" aria-hidden="true" />
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Pagination footer */}
            <div className="flex items-center justify-between border-t border-surface-border px-4 py-3 text-sm text-ink-muted">
              <span>
                Page {page} of {totalPages} · {totalCount} total request{totalCount === 1 ? '' : 's'}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1 || loading}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page >= totalPages || loading}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </Card>

      {/* Detail Modal */}
      <RequestDetailModal
        open={Boolean(selectedRequest)}
        request={selectedRequest}
        onClose={() => setSelectedRequest(null)}
        onSubmit={handleSubmitDraft}
        onWithdraw={handleWithdraw}
        onRequestCancellation={(req) => {
          setSelectedRequest(null)
          setCancellationTarget(req)
        }}
        onDelete={handleDeleteDraft}
        isActioning={isActioning}
      />

      {/* Cancellation Request Modal */}
      <CancellationModal
        open={Boolean(cancellationTarget)}
        request={cancellationTarget}
        onClose={() => setCancellationTarget(null)}
        onConfirm={handleConfirmCancellation}
        isSubmitting={isActioning}
      />
    </div>
  )
}
