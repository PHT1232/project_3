import { formatCurrency, formatNumber } from '../../../lib/format.js'
import LineChart from './charts/LineChart.jsx'

/**
 * Report 3 — approved spend per calendar month and the running cumulative total.
 * Two single-series line charts (small multiples — never a dual-axis chart), the
 * monthly figures, and a "Top Consumed Items" usage snapshot for the same period
 * (`report.topConsumed`, top 5 by units approved).
 */
export default function CumulativeCostView({ report }) {
  const { points, totalApprovedCost, topConsumed } = report

  const monthly = points.map((p) => ({ label: p.periodLabel, value: p.periodCost }))
  const cumulative = points.map((p) => ({ label: p.periodLabel, value: p.cumulativeCost }))

  return (
    <div className="space-y-8 p-4">
      <section>
        <h3 className="mb-2 text-sm font-semibold text-ink">Approved spend per month</h3>
        <LineChart points={monthly} format={formatCurrency} ariaLabel="Approved spend per month" />
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold text-ink">
          Cumulative approved spend — {formatCurrency(totalApprovedCost)} to date
        </h3>
        <LineChart
          points={cumulative}
          format={formatCurrency}
          ariaLabel="Cumulative approved spend over time"
        />
      </section>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-surface-border text-left text-xs font-semibold uppercase tracking-wide text-ink-muted">
              <th scope="col" className="px-4 py-3">Month</th>
              <th scope="col" className="px-4 py-3 text-right">Approved Cost</th>
              <th scope="col" className="px-4 py-3 text-right">Cumulative</th>
            </tr>
          </thead>
          <tbody>
            {points.map((point) => (
              <tr key={point.periodKey} className="border-b border-surface-border last:border-0">
                <td className="px-4 py-3 font-medium text-ink">{point.periodLabel}</td>
                <td className="px-4 py-3 text-right tabular-nums text-ink-muted">
                  {formatCurrency(point.periodCost)}
                </td>
                <td className="px-4 py-3 text-right tabular-nums font-semibold text-ink">
                  {formatCurrency(point.cumulativeCost)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {topConsumed && topConsumed.length > 0 && (
        <section>
          <h3 className="mb-2 text-sm font-semibold text-ink">Top Consumed Items This Period</h3>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-surface-border text-left text-xs font-semibold uppercase tracking-wide text-ink-muted">
                  <th scope="col" className="px-4 py-3">Item</th>
                  <th scope="col" className="px-4 py-3">Category</th>
                  <th scope="col" className="px-4 py-3 text-right">Units Approved</th>
                  <th scope="col" className="px-4 py-3 text-right">Approved Cost</th>
                </tr>
              </thead>
              <tbody>
                {topConsumed.map((item) => (
                  <tr key={item.itemName} className="border-b border-surface-border last:border-0">
                    <td className="px-4 py-3 font-medium text-ink">{item.itemName}</td>
                    <td className="px-4 py-3 text-ink-muted">{item.categoryName}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-ink">
                      {formatNumber(item.unitsApproved)}
                    </td>
                    <td className="px-4 py-3 text-right tabular-nums text-ink-muted">
                      {formatCurrency(item.approvedCost)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}
