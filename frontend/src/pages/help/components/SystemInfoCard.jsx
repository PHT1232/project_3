import Card from '../../../components/ui/Card.jsx'
import { useAuth } from '../../../contexts/AuthContext.jsx'
import { formatDate } from '../../../lib/format.js'

const RANKS = { 1: 'Engineer', 2: 'Manager', 3: 'Business Manager', 4: 'Managing Director' }

/** Read-only build + session facts, handy to quote in a bug report. */
export default function SystemInfoCard() {
  const { user } = useAuth()

  const rows = [
    ['Signed in as', user ? `#${user.employeeNumber} · ${user.name}` : '—'],
    ['Role', user ? `${user.role}${RANKS[user.rankLevel] && RANKS[user.rankLevel] !== user.role ? ` (rank ${user.rankLevel})` : ''}` : '—'],
    ['App version', __APP_VERSION__],
    ['Build date', formatDate(__BUILD_TIME__)],
  ]

  return (
    <Card className="p-5">
      <h2 className="text-base font-semibold text-ink">System info</h2>
      <dl className="mt-3 space-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-4">
            <dt className="text-ink-muted">{label}</dt>
            <dd className="text-right font-medium text-ink">{value}</dd>
          </div>
        ))}
      </dl>
    </Card>
  )
}
