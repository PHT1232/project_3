import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

import Card from '../../../components/ui/Card.jsx'
import Button from '../../../components/ui/Button.jsx'
import { EmptyState } from '../../../components/ui/StateBlock.jsx'
import RequestStatusBadge from '../../requests/components/RequestStatusBadge.jsx'
import { formatCurrency, formatDate } from '../../../lib/format.js'
import RequestTimeframe, { DEFAULT_TIMEFRAME, resolveTimeframeWindow } from './RequestTimeframe.jsx'

/**
 * "Recent Requests" panel from the wireframe. `requests` is the most-recent page of requests
 * visible to the caller (own, pending their approval, or raised anywhere in their reporting
 * sub-tree) — GET /requests already scopes and orders this newest-first.
 *
 * A time-frame control (this week / this month / custom From→To) narrows the list
 * client-side, since GET /requests takes no date parameter. The result scrolls inside a
 * fixed-height area rather than growing the page. "View All" goes to My Requests.
 *
 * The wireframe's "REQ-2039" style id is a mock-up convention; `RequestDto.requestId` is a
 * bare int, so we render `#{id}`.
 */
export default function RecentRequestsCard({ requests }) {
  const [timeframe, setTimeframe] = useState(DEFAULT_TIMEFRAME)

  const visible = useMemo(() => {
    const { fromMs, toMs } = resolveTimeframeWindow(timeframe)
    return requests.filter((request) => {
      const t = new Date(request.createdAtUtc).getTime()
      return t >= fromMs && t <= toMs
    })
  }, [requests, timeframe])

  return (
    <Card className="overflow-hidden">
      <div className="border-b border-surface-border px-5 py-4">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-ink">Recent Requests</h2>
          <Button as={Link} to="/my-requests" size="sm">
            View All
          </Button>
        </div>
        <div className="mt-3">
          <RequestTimeframe value={timeframe} onChange={setTimeframe} />
        </div>
      </div>

      {visible.length === 0 ? (
        <EmptyState
          title={requests.length === 0 ? 'No requests yet' : 'No requests in this period'}
          description={
            requests.length === 0
              ? 'Requests you raise — and ones awaiting your approval — will show here.'
              : 'Try a wider time frame.'
          }
        />
      ) : (
        <div className="max-h-96 overflow-y-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 z-10 bg-surface-card">
              <tr className="border-b border-surface-border text-left text-xs font-semibold uppercase tracking-wide text-ink-muted">
                <th scope="col" className="px-5 py-3">ID</th>
                <th scope="col" className="px-5 py-3">Requester</th>
                <th scope="col" className="px-5 py-3">Date</th>
                <th scope="col" className="px-5 py-3">Status</th>
                <th scope="col" className="px-5 py-3 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              {visible.map((request) => (
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
