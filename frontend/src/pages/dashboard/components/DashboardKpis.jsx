import { ClipboardCheck, AlertTriangle, Wallet } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import { formatCurrency, formatNumber } from '../../../lib/format.js'

/**
 * The three KPI cards from the approved Dashboard wireframe. Each is label + big figure +
 * a supporting line, with an icon tile on the right.
 *
 * `danger` tints the figure red (Low Stock with alerts; Remaining Budget when nearly spent).
 * `muted` renders a placeholder instead of a figure — used when the data is gated (Low Stock
 * is Manager+) or unavailable (the eligibility call failed).
 */
function KpiCard({ icon: Icon, label, value, hint, danger = false, muted = false }) {
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between gap-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{label}</p>
        <span
          className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
            danger ? 'bg-status-dangerBg text-status-danger' : 'bg-brand-50 text-brand-700'
          }`}
          aria-hidden="true"
        >
          <Icon className="h-5 w-5" />
        </span>
      </div>
      <p
        className={`mt-2 text-3xl font-bold tracking-tight ${
          muted ? 'text-ink-subtle' : danger ? 'text-status-danger' : 'text-ink'
        }`}
      >
        {value}
      </p>
      <p className="mt-2 text-sm text-ink-muted">{hint}</p>
    </Card>
  )
}

export default function DashboardKpis({ pendingApprovals, lowStockCount, isManager, eligibility }) {
  const monthly = eligibility?.maxAmountPerMonth ?? 0
  const budgetPct = eligibility && monthly > 0
    ? Math.round((eligibility.remainingThisMonth / monthly) * 100)
    : null

  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
      <KpiCard
        icon={ClipboardCheck}
        label="Pending Approvals"
        value={formatNumber(pendingApprovals)}
        hint="Requires your review"
      />
      <KpiCard
        icon={AlertTriangle}
        label="Low Stock Alerts"
        value={isManager ? formatNumber(lowStockCount) : '—'}
        hint={isManager ? 'Inventory items at or below reorder level' : 'Manager view only'}
        danger={isManager && lowStockCount > 0}
        muted={!isManager}
      />
      <KpiCard
        icon={Wallet}
        label="Remaining Budget"
        value={eligibility ? formatCurrency(eligibility.remainingThisMonth) : '—'}
        hint={
          eligibility
            ? `${budgetPct}% of your ${formatCurrency(monthly)} monthly allowance`
            : 'Not available'
        }
        danger={budgetPct != null && budgetPct < 10}
        muted={!eligibility}
      />
    </div>
  )
}
