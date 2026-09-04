import Card from '../../../components/ui/Card.jsx'
import { Skeleton } from '../../../components/ui/Skeleton.jsx'
import { MAX_COST_CAP } from '../filters.js'

/** At the cap the ceiling is open-ended, matching the "$100+" track label. */
function formatCostLabel(cost) {
  return cost >= MAX_COST_CAP ? `$${MAX_COST_CAP}+` : `$${cost}`
}

/** Approx. native range-thumb width; used to keep the badge centred over the thumb at both ends. */
const THUMB_WIDTH = 16

function FieldSet({ legend, children }) {
  return (
    <fieldset className="border-0 p-0">
      <legend className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-muted">
        {legend}
      </legend>
      {children}
    </fieldset>
  )
}

/**
 * Unit-cost ceiling slider with a value badge pinned above the thumb, so the exact
 * amount being filtered on is always visible while dragging.
 */
function UnitCostSlider({ cost, onCostChange }) {
  const percent = (cost / MAX_COST_CAP) * 100
  // Shift the badge from +½thumb at the far left to −½thumb at the far right so it
  // tracks the thumb centre rather than the input's edge-to-edge box.
  const thumbOffset = (0.5 - percent / 100) * THUMB_WIDTH
  const costLabel = formatCostLabel(cost)

  return (
    <div className="relative pt-9">
      <div
        className="pointer-events-none absolute top-0 -translate-x-1/2 transition-[left] duration-75 ease-out"
        style={{ left: `calc(${percent}% + ${thumbOffset}px)` }}
        aria-hidden="true"
      >
        <div className="relative whitespace-nowrap rounded-md bg-brand-700 px-2 py-1 text-xs font-semibold tabular-nums text-white shadow-sm">
          {costLabel}
          <span className="absolute left-1/2 top-full h-2 w-2 -translate-x-1/2 -translate-y-1/2 rotate-45 bg-brand-700" />
        </div>
      </div>
      <input
        type="range"
        min={0}
        max={MAX_COST_CAP}
        step={5}
        value={cost}
        aria-label="Maximum unit cost"
        aria-valuetext={costLabel}
        onChange={(e) => onCostChange(Number(e.target.value))}
        className="w-full accent-brand-700"
      />
    </div>
  )
}

/**
 * Catalogue filter panel — category, availability and a unit-cost ceiling, exactly the three
 * groups on the approved wireframe.
 */
export default function CatalogueFilters({
  categories,
  suppliers,
  value,
  onChange,
  loading = false,
}) {
  const allSelected = value.categoryIds.length === 0

  function toggleCategory(categoryId) {
    const next = value.categoryIds.includes(categoryId)
      ? value.categoryIds.filter((id) => id !== categoryId)
      : [...value.categoryIds, categoryId]
    onChange({ ...value, categoryIds: next })
  }

  return (
    <Card className="space-y-6 p-5">
      <FieldSet legend="Category">
        <div className="space-y-2.5">
          <label className="flex cursor-pointer items-center gap-2.5 text-sm text-ink">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={() => onChange({ ...value, categoryIds: [] })}
              className="h-4 w-4 rounded border-surface-border text-brand-700 focus:ring-brand-500"
            />
            All Categories
          </label>
          {/* Placeholder rows while the category list loads, so the panel keeps its height. */}
          {loading &&
            Array.from({ length: 4 }, (_, index) => (
              <div key={index} className="flex items-center gap-2.5 py-1">
                <Skeleton className="h-4 w-4" />
                <Skeleton className="h-3 w-28" />
              </div>
            ))}
          {categories.map((category) => (
            <label
              key={category.categoryId}
              className="flex cursor-pointer items-center gap-2.5 text-sm text-ink"
            >
              <input
                type="checkbox"
                checked={value.categoryIds.includes(category.categoryId)}
                onChange={() => toggleCategory(category.categoryId)}
                className="h-4 w-4 rounded border-surface-border text-brand-700 focus:ring-brand-500"
              />
              {category.name}
            </label>
          ))}
        </div>
      </FieldSet>

      <hr className="border-surface-border" />

      <FieldSet legend="Supplier">
        <select
          aria-label="Supplier"
          value={value.supplierId}
          onChange={(e) => onChange({ ...value, supplierId: e.target.value })}
          className="w-full rounded-md border border-surface-border bg-surface-card px-3 py-2 text-sm text-ink"
        >
          <option value="">All suppliers</option>
          {suppliers.map((supplier) => (
            <option key={supplier.supplierId} value={supplier.supplierId}>
              {supplier.name}
            </option>
          ))}
        </select>
      </FieldSet>

      <hr className="border-surface-border" />

      <FieldSet legend="Availability">
        <div className="space-y-2.5">
          {[
            { id: 'ALL', label: 'Show All' },
            { id: 'IN_STOCK', label: 'In Stock Only' },
          ].map((option) => (
            <label
              key={option.id}
              className="flex cursor-pointer items-center gap-2.5 text-sm text-ink"
            >
              <input
                type="radio"
                name="availability"
                checked={value.availability === option.id}
                onChange={() => onChange({ ...value, availability: option.id })}
                className="h-4 w-4 border-surface-border text-brand-700 focus:ring-brand-500"
              />
              {option.label}
            </label>
          ))}

          {/*
            "Available to Me" filters by MinRankLevelToRequest against the signed-in user's
            RankLevel ([ASK] #3 default). That requires the current user, which comes from the
            AuthContext M1 owns (Plan T1.8) and does not exist yet. Disabled rather than
            guessing a rank — a wrong default would silently show the wrong catalogue.
          */}
          <label
            className="flex items-center gap-2.5 text-sm text-ink-subtle"
            title="Requires the signed-in user's rank level — pending the auth context (M1)"
          >
            <input
              type="radio"
              name="availability"
              disabled
              checked={false}
              readOnly
              className="h-4 w-4 border-surface-border"
            />
            Available to Me
          </label>
        </div>
      </FieldSet>

      <hr className="border-surface-border" />

      <FieldSet legend="Unit Cost Max">
        <UnitCostSlider
          cost={value.maxUnitCost}
          onCostChange={(maxUnitCost) => onChange({ ...value, maxUnitCost })}
        />
        <div className="mt-1 flex justify-between text-xs text-ink-muted">
          <span>$0</span>
          <span>$50</span>
          <span>$100+</span>
        </div>
      </FieldSet>
    </Card>
  )
}
