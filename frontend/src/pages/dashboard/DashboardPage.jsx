import PageHeader from '../../components/layout/PageHeader.jsx'
import { LoadingState, ErrorState } from '../../components/ui/StateBlock.jsx'
import useAsync from '../../hooks/useAsync.js'
import { useAuth } from '../../contexts/AuthContext.jsx'
import { getPendingApprovals, getRequests } from '../../api/requests.js'
import { getLowStock } from '../../api/inventory.js'

import DashboardKpis from './components/DashboardKpis.jsx'
import RecentRequestsCard from './components/RecentRequestsCard.jsx'
import LowStockPanel from './components/LowStockPanel.jsx'

const RECENT_LIMIT = 5

/**
 * Home dashboard (`/`). NOT in the Plan's endpoint catalogue — composed entirely from
 * endpoints that already exist (page-map §3): pending-approval count, recent visible
 * requests, and (Manager+ only) low-stock inventory. "Remaining Budget" from the wireframe
 * has no data source yet and renders as a placeholder — see DashboardKpis.
 */
export default function DashboardPage() {
  const { user } = useAuth()
  const isManager = (user?.rankLevel ?? 0) >= 2

  const { data, error, loading, reload } = useAsync(
    () =>
      Promise.all([
        getPendingApprovals({ page: 1, pageSize: 1 }),
        getRequests({ page: 1, pageSize: RECENT_LIMIT }),
        isManager ? getLowStock() : Promise.resolve([]),
      ]).then(([pending, recent, lowStock]) => ({
        pendingApprovals: pending.totalCount ?? 0,
        recentRequests: recent.items ?? [],
        lowStock: lowStock ?? [],
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
