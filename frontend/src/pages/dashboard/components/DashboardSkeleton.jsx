import Card from '../../../components/ui/Card.jsx'
import { Skeleton, SkeletonStatCards, SkeletonTable } from '../../../components/ui/Skeleton.jsx'

/**
 * Loading shape for the dashboard: the KPI row, the Recent Requests table and — for a
 * Manager+ — the Low Stock side panel, laid out on the same grid as the real content so
 * nothing moves when the data lands.
 *
 * Page-specific because the dashboard is the only screen that composes all three at once;
 * the pieces themselves come from the shared skeleton primitives.
 */
export default function DashboardSkeleton({ isManager }) {
  return (
    <div className="space-y-6">
      <SkeletonStatCards label="Loading your dashboard…" count={3} kpi />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className={isManager ? 'lg:col-span-2' : 'lg:col-span-3'}>
          <Card className="overflow-hidden">
            <div className="flex items-center justify-between border-b border-surface-border px-5 py-4">
              <Skeleton className="h-4 w-36" />
              <Skeleton className="h-9 w-24 rounded-md" />
            </div>
            <SkeletonTable
              label="Loading recent requests…"
              rows={5}
              cellClassName="px-5 py-3"
              columns={[3, 5, 6, { width: 6, height: 'h-6' }, { width: 4, align: 'right' }]}
            />
          </Card>
        </div>

        {isManager && (
          <div className="lg:col-span-1">
            <Card className="overflow-hidden">
              <div className="flex items-center justify-between border-b border-status-dangerBg bg-status-dangerBg/40 px-5 py-4">
                <Skeleton className="h-4 w-32" />
                <Skeleton className="h-5 w-5 rounded-full" />
              </div>
              <div className="space-y-3 p-4" role="status" aria-busy="true">
                <span className="sr-only">Loading low stock alerts…</span>
                {Array.from({ length: 3 }, (_, index) => (
                  <div key={index} className="rounded-card border border-surface-border p-4">
                    <div className="flex items-start justify-between gap-3">
                      <Skeleton className="h-4 w-32" />
                      <Skeleton className="h-4 w-16 shrink-0" />
                    </div>
                    <Skeleton className="mt-3 h-1.5 w-full rounded-full" />
                    <div className="mt-3 flex items-center justify-between">
                      <Skeleton className="h-3 w-28" />
                      <Skeleton className="h-8 w-20 rounded-md" />
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          </div>
        )}
      </div>
    </div>
  )
}
