import Card from './Card.jsx'

/** Shared KPI tile: uppercase label above a large figure. Used by the Inventory summary row. */
export default function StatCard({ label, value }) {
  return (
    <Card className="px-5 py-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{label}</p>
      <p className="mt-2 text-3xl font-bold tracking-tight text-ink">{value}</p>
    </Card>
  )
}
