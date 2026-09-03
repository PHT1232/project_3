import { Link } from 'react-router-dom'
import { AlertTriangle } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import Button from '../../../components/ui/Button.jsx'
import { formatNumber } from '../../../lib/format.js'

/**
 * "Low Stock Alerts" side panel from the wireframe. Manager+ only (GET /inventory/low-stock
 * is RequireManager) — the parent decides whether to render this at all.
 *
 * Deviations from the mock-up, both per page-map §3:
 *  - no SKU line — SKU is a Plan *future improvement*, not in InventoryRowDto.
 *  - "Reorder" links to /inventory (where the real Adjust / Receive Goods actions live);
 *    there is no reorder endpoint.
 *
 * The bar shows quantity-on-hand against the reorder level (clamped to that as the full width),
 * so a near-empty item reads as an almost-empty bar.
 */
function LowStockItem({ item }) {
  const ratio = item.reorderLevel > 0 ? Math.min(item.quantityAvailable / item.reorderLevel, 1) : 0

  return (
    <div className="rounded-card border border-surface-border p-4">
      <div className="flex items-start justify-between gap-3">
        <p className="font-medium leading-snug text-ink">{item.itemName}</p>
        <span className="shrink-0 rounded bg-status-dangerBg px-1.5 py-0.5 text-xs font-semibold text-status-danger">
          {formatNumber(item.quantityAvailable)} left
        </span>
      </div>

      <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-surface-muted" aria-hidden="true">
        <div className="h-full rounded-full bg-status-danger" style={{ width: `${ratio * 100}%` }} />
      </div>

      <div className="mt-3 flex items-center justify-between">
        <span className="text-xs text-ink-muted">Reorder level: {formatNumber(item.reorderLevel)}</span>
        <Button as={Link} to="/inventory" variant="secondary" size="sm">
          Reorder
        </Button>
      </div>
    </div>
  )
}

export default function LowStockPanel({ items }) {
  return (
    <Card className="overflow-hidden">
      <div className="flex items-center justify-between border-b border-status-dangerBg bg-status-dangerBg/40 px-5 py-4">
        <h2 className="text-base font-semibold text-ink">Low Stock Alerts</h2>
        <AlertTriangle className="h-5 w-5 text-status-danger" aria-hidden="true" />
      </div>

      {items.length === 0 ? (
        <p className="px-5 py-8 text-center text-sm text-ink-muted">
          Nothing below its reorder level right now.
        </p>
      ) : (
        <div className="max-h-96 space-y-3 overflow-y-auto p-4">
          {items.map((item) => (
            <LowStockItem key={item.itemId} item={item} />
          ))}
        </div>
      )}
    </Card>
  )
}
