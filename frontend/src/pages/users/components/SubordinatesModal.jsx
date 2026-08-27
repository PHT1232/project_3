import Modal from '../../../components/ui/Modal.jsx'
import { LoadingState, ErrorState, EmptyState } from '../../../components/ui/StateBlock.jsx'
import useAsync from '../../../hooks/useAsync.js'
import { getSubordinates } from '../../../api/users.js'

export default function SubordinatesModal({ open, onClose, user }) {
  const { data, error, loading, reload } = useAsync(
    () => (open && user ? getSubordinates(user.employeeNumber) : Promise.resolve([])),
    [open, user?.employeeNumber],
  )

  if (!user) return null

  return (
    <Modal open={open} onClose={onClose} title={`Direct reports — ${user.name}`}>
      {loading && <LoadingState label="Loading direct reports…" />}
      {!loading && error && <ErrorState error={error} onRetry={reload} />}
      {!loading && !error && data?.length === 0 && (
        <EmptyState title="No direct reports" description="This user has nobody reporting to them." />
      )}
      {!loading && !error && data?.length > 0 && (
        <ul className="divide-y divide-surface-border">
          {data.map((subordinate) => (
            <li key={subordinate.employeeNumber} className="flex items-center justify-between py-2">
              <div>
                <p className="text-sm font-semibold text-ink">{subordinate.name}</p>
                <p className="text-xs text-ink-muted">
                  #{subordinate.employeeNumber} · {subordinate.role}
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Modal>
  )
}
