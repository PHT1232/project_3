import { ArrowRight, ShoppingCart } from 'lucide-react'

import Button from '../../../components/ui/Button.jsx'
import { formatCurrency } from '../../../lib/format.js'

/**
 * Summary bar for items picked from the catalogue: how many, their estimated total, and the
 * action that carries them into the New Request page (Plan T2.4, "add to request").
 *
 * Renders nothing when nothing is selected, so the grid is unobstructed until the user picks
 * something. Sticks to the bottom of the viewport so the totals stay visible while scrolling a
 * long catalogue.
 */
export default function CatalogueSelectionBar({ items, onClear, onProceed }) {
  if (items.length === 0) {
    return null
  }

  const estimatedTotal = items.reduce((sum, item) => sum + (item.unitCost ?? 0), 0)

  return (
    <div className="sticky bottom-4 z-10 mt-6">
      <div className="flex flex-col gap-3 rounded-card border border-surface-border bg-surface-card p-4 shadow-lg sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <ShoppingCart className="h-5 w-5 shrink-0 text-ink-muted" aria-hidden="true" />
          <p className="text-sm text-ink">
            <span className="font-bold">
              {items.length} item{items.length === 1 ? '' : 's'}
            </span>{' '}
            selected
            <span className="text-ink-muted"> · est. </span>
            <span className="font-bold">{formatCurrency(estimatedTotal)}</span>
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button variant="secondary" onClick={onClear}>
            Clear
          </Button>
          <Button onClick={onProceed}>
            Proceed
            <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </Button>
        </div>
      </div>
    </div>
  )
}
