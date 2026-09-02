import { ShoppingCart, Check, BellRing, Laptop, FileText, PenLine, Archive, Package } from 'lucide-react'

import Card from '../../../components/ui/Card.jsx'
import Badge from '../../../components/ui/Badge.jsx'
import Button from '../../../components/ui/Button.jsx'
import { formatCurrency } from '../../../lib/format.js'
import { getAvailability, getAvailabilityLabel, AVAILABILITY } from '../../../lib/availability.js'

/**
 * Category icons. The Plan (§7 M2, risk register) explicitly rules item images out of scope
 * and says to use a category icon from `lucide-react` instead.
 */
const CATEGORY_ICONS = {
  'Tech & Accessories': Laptop,
  'Paper & Notebooks': FileText,
  'Writing Instruments': PenLine,
  Organization: Archive,
}

const AVAILABILITY_BADGE = {
  [AVAILABILITY.IN_STOCK]: { tone: 'plain', dot: 'info' },
  [AVAILABILITY.LOW_STOCK]: { tone: 'danger', dot: 'danger' },
  [AVAILABILITY.OUT_OF_STOCK]: { tone: 'plain', dot: 'subtle' },
}

export default function ItemCard({ item, onAdd, added = false }) {
  const availability = getAvailability(item)
  const outOfStock = availability === AVAILABILITY.OUT_OF_STOCK
  const Icon = CATEGORY_ICONS[item.categoryName] ?? Package
  const badge = AVAILABILITY_BADGE[availability]

  return (
    <Card className="flex flex-col overflow-hidden">
      <div className="relative flex h-40 items-center justify-center bg-surface-muted">
        {/*
          UNSPECIFIED: "MGR APPROVAL REQ" is on the approved wireframe but has no rule, entity
          or endpoint anywhere in the Plan or ERD (K5 in CLAUDE.md §6). Rendered from a data
          flag so no client-side rule is invented for it.
        */}
        {item.requiresManagerApproval && (
          <Badge tone="solid" className="absolute left-3 top-3">
            MGR APPROVAL REQ
          </Badge>
        )}
        <Badge tone={badge.tone} dot={badge.dot} className="absolute right-3 top-3">
          {getAvailabilityLabel(item)}
        </Badge>
        <Icon className="h-10 w-10 text-ink-subtle" aria-hidden="true" />
      </div>

      <div className="flex flex-1 flex-col p-4">
        <h3 className="text-base font-bold leading-snug text-ink">{item.itemName}</h3>
        <p className="mt-1 text-xs text-ink-muted">
          {item.categoryName} • {item.unitOfMeasure}
        </p>

        <hr className="my-4 border-surface-border" />

        <div className="mt-auto flex items-end justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase leading-tight text-ink-muted">Est. Cost</p>
            <p className="text-lg font-bold text-ink">{formatCurrency(item.unitCost)}</p>
          </div>

          {outOfStock ? (
            /*
              "Notify Me" is on the wireframe but there is no back-in-stock subscription entity
              or endpoint in the Plan (K5). Rendered disabled rather than wired to nothing.
            */
            <Button
              variant="muted"
              disabled
              title="Back-in-stock notifications are not specified in the Plan"
            >
              <BellRing className="h-4 w-4" aria-hidden="true" />
              Notify Me
            </Button>
          ) : (
            /*
              Adds the item to the page's selection (Plan T2.4 completes this in M3). Quantities
              are edited on the New Request page, which already owns that input, so a second click
              is a no-op rather than a duplicate line — the API rejects duplicate item lines.
            */
            <Button
              variant={added ? 'secondary' : 'primary'}
              disabled={added}
              onClick={() => onAdd(item)}
              aria-label={`Add ${item.itemName} to a request`}
            >
              {added ? (
                <Check className="h-4 w-4" aria-hidden="true" />
              ) : (
                <ShoppingCart className="h-4 w-4" aria-hidden="true" />
              )}
              {added ? 'Added' : item.requiresManagerApproval ? 'Request' : 'Add Request'}
            </Button>
          )}
        </div>
      </div>
    </Card>
  )
}
