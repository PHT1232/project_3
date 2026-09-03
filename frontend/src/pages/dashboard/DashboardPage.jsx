import PageHeader from '../../components/layout/PageHeader.jsx'
import { LoadingState, ErrorState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { useAuth } from '../../contexts/AuthContext.jsx'
import { getPendingApprovals, getRequests } from '../../api/requests.js'
import { getLowStock } from '../../api/inventory.js'
import { getMyEligibility } from '../../api/users.js'

import DashboardKpis from './components/DashboardKpis.jsx'
import RecentRequestsCard from './components/RecentRequestsCard.jsx'
import LowStockPanel from './components/LowStockPanel.jsx'

// GET /requests has no date filter, so fetch a generous page of the newest requests and let
// RecentRequestsCard's time-frame control narrow it client-side.
const RECENT_FETCH = 100

/**
 * Home dashboard (`/`). NOT in the Plan's endpoint catalogue — composed entirely from
 * endpoints that already exist (page-map §3): pending-approval count, recent visible
 * requests, (Manager+ only) low-stock inventory, and the caller's spending eligibility.
 * The eligibility call is caught individually so a hiccup there can't blank the whole
 * dashboard — the Remaining Budget tile just falls back to its placeholder.
 */
export default function DashboardPage() {
  const { user } = useAuth()
  const isManager = (user?.rankLevel ?? 0) >= 2

  const { data, error, loading, reload } = useAsync(
    () =>
      Promise.all([
        getPendingApprovals({ page: 1, pageSize: 1 }),
        getRequests({ page: 1, pageSize: RECENT_FETCH }),
        isManager ? getLowStock() : Promise.resolve([]),
        getMyEligibility().catch(() => null),
      ]).then(([pending, recent, lowStock, eligibility]) => ({
        pendingApprovals: pending.totalCount ?? 0,
        recentRequests: recent.items ?? [],
        lowStock: lowStock ?? [],
        eligibility,
      })),
    [isManager],
  )

  return (
    <>
      <PageHeader
        title={user?.name ? `Welcome back, ${user.name}` : 'Dashboard'}
        description="Your approvals, recent request activity and stock alerts at a glance."
      />

      {loading && <LoadingState label="Loading your dashboard…" />}

      {!loading && error && <ErrorState error={error} onRetry={reload} />}

      {!loading && !error && data && (
        <div className="space-y-6">
          <DashboardKpis
            pendingApprovals={data.pendingApprovals}
            lowStockCount={data.lowStock.length}
            isManager={isManager}
            eligibility={data.eligibility}
          />

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
            <div className={isManager ? 'lg:col-span-2' : 'lg:col-span-3'}>
              <RecentRequestsCard requests={data.recentRequests} />
            </div>
            {isManager && (
              <div className="lg:col-span-1">
                <LowStockPanel items={data.lowStock} />
              </div>
            )}
          </div>
        </div>
      )}
    </>
  )
}
