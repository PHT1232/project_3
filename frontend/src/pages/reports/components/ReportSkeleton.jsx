import { Skeleton, SkeletonTable } from '../../../components/ui/Skeleton.jsx'

/**
 * Loading shape shared by every report tab: each view renders a chart band above a figures
 * table (see CostByItemView and friends), so one placeholder covers them all and the card
 * keeps its height while a tab switch refetches.
 */
export default function ReportSkeleton({ label = 'Loading report…' }) {
  return (
    <div className="space-y-6 p-4">
      <Skeleton className="h-48 w-full" />
      <SkeletonTable
        label={label}
        rows={6}
        columns={[5, 3, { width: 3, align: 'right' }, { width: 2, align: 'right' }]}
      />
    </div>
  )
}
