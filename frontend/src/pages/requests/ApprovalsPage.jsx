import { useState } from 'react'
import { ClipboardCheck } from 'lucide-react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { formatCurrency, formatDate } from '../../lib/format.js'
import { getPendingApprovals } from '../../api/requests.js'

import RequestStatusBadge from './components/RequestStatusBadge.jsx'
import RequestReviewModal from './components/RequestReviewModal.jsx'

const PAGE_SIZE = 20

/**
 * Approver's pending-request queue. Plan §3.6/§4.2, wireframe docs/Wireframe/Approvals.png.
 *
 * The wireframe's "Department" column/filter has no backing field on RequestDto or a Plan
 * concept behind it (same K5 status as Catalogue's unimplemented filters) — omitted here
 * rather than invented.
 *
 * Cancellation-approval (POST /approvals/{id}/cancel-approval) is not wired: the only list
 * endpoint, GET /approvals/pending, filters to Status == "Pending" only, so there is currently
 * no way for this page to discover which requests are awaiting a cancellation decision. See
 * docs/development/request-approval-frontend-implementation-plan.md.
 */
export default function ApprovalsPage() {
  const [page, setPage] = useState(1)
  const [reviewing, setReviewing] = useState(null)

  const { data, error, loading, reload } = useAsync(
    () => getPendingApprovals({ page, pageSize: PAGE_SIZE }),
    [page],
  )

  const requests = data?.items ?? []
  const totalCount = data?.totalCount ?? 0
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  return (
    <>
      <PageHeader
        title="Approvals"
        description="Review and decide on stationery requests awaiting your approval."
      />

      <Card>
        {loading && <LoadingState label="Loading pending approvals…" />}
        {!loading && error && <ErrorState error={error} onRetry={reload} />}
        {!loading && !error && requests.length === 0 && (
          <EmptyState
            title="Nothing pending"
            description="No requests are currently waiting on your approval."
          />
        )}
        {!loading && !error && requests.length > 0 && (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-surface-border text-xs uppercase tracking-wide text-ink-muted">
                    <th className="px-4 py-3 font-semibold">Requester</th>
                    <th className="px-4 py-3 font-semibold">Date submitted</th>
                    <th className="px-4 py-3 font-semibold">Items</th>
                    <th className="px-4 py-3 font-semibold">Est. cost</th>
                    <th className="px-4 py-3 font-semibold">Status</th>
                    <th className="px-4 py-3 font-semibold text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-surface-border">
                  {requests.map((request) => (
                    <tr key={request.requestId}>
                      <td className="px-4 py-3 font-medium text-ink">
                        {request.requestorName ?? `#${request.requestorEmployeeNumber}`}
                      </td>
                      <td className="px-4 py-3 text-ink-muted">{formatDate(request.createdAtUtc)}</td>
                      <td className="px-4 py-3 text-ink-muted">
                        {request.items.length} item{request.items.length === 1 ? '' : 's'}
                      </td>
                      <td className="px-4 py-3 text-ink-muted">{formatCurrency(request.totalEstimatedCost)}</td>
                      <td className="px-4 py-3">
                        <RequestStatusBadge status={request.status} />
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Button size="sm" onClick={() => setReviewing(request)}>
                          <ClipboardCheck className="h-4 w-4" aria-hidden="true" />
                          Review
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="flex items-center justify-between border-t border-surface-border px-4 py-3 text-sm text-ink-muted">
              <span>
                Page {page} of {totalPages} · {totalCount} request{totalCount === 1 ? '' : 's'}
              </span>
              <div className="flex gap-2">
                <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </Card>

      <RequestReviewModal
        open={Boolean(reviewing)}
        request={reviewing}
        onClose={() => setReviewing(null)}
        onSuccess={reload}
      />
    </>
  )
}
