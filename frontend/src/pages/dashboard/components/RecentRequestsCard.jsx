import { Link } from 'react-router-dom'

import Card from '../../../components/ui/Card.jsx'
import Button from '../../../components/ui/Button.jsx'
import { EmptyState } from '../../../components/ui/StateBlock.jsx'
import RequestStatusBadge from '../../requests/components/RequestStatusBadge.jsx'
import { formatCurrency, formatDate } from '../../../lib/format.js'

/**
 * "Recent Requests" panel from the wireframe. Shows the 5 most recent requests visible to the
 * caller (own, or own + subordinates for an approver, or all for a Manager) — GET /requests
 * already scopes this. "View All" goes to My Requests.
 *
 * The wireframe's "REQ-2039" style id is a mock-up convention; `RequestDto.requestId` is a
 * bare int, so we render `#{id}`.
 */
export default function RecentRequestsCard({ requests }) {
  return (
    <Card className="overflow-hidden">
      <div className="flex items-center justify-between border-b border-surface-border px-5 py-4">
        <h2 className="text-base font-semibold text-ink">Recent Requests</h2>
        <Button as={Link} to="/my-requests" size="sm">
          View All
        </Button>
      </div>

      {requests.length === 0 ? (
        <EmptyState
          title="No requests yet"
          description="Requests you raise — and ones awaiting your approval — will show here."
        />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-surface-border text-left text-xs font-semibold uppercase tracking-wide text-ink-muted">
                <th scope="col" className="px-5 py-3">ID</th>
                <th scope="col" className="px-5 py-3">Requester</th>
                <th scope="col" className="px-5 py-3">Date</th>
                <th scope="col" className="px-5 py-3">Status</th>
                <th scope="col" className="px-5 py-3 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((request) => (
                <tr key={request.requestId} className="border-b border-surface-border last:border-0">
                  <td className="px-5 py-3 font-medium text-ink">
                    <Link to="/my-requests" className="hover:underline">
                      #{request.requestId}
                    </Link>
                  </td>
                  <td className="px-5 py-3 text-ink">{request.requestorName ?? '—'}</td>
                  <td className="px-5 py-3 text-ink-muted">{formatDate(request.createdAtUtc)}</td>
                  <td className="px-5 py-3">
                    <RequestStatusBadge status={request.status} />
                  </td>
                  <td className="px-5 py-3 text-right tabular-nums text-ink">
                    {formatCurrency(request.totalEstimatedCost)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}
