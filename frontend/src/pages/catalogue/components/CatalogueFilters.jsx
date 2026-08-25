import Card from '../../../components/ui/Card.jsx'
import { MAX_COST_CAP } from '../filters.js'

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
 * Catalogue filter panel — category, availability and a unit-cost ceiling, exactly the three
 * groups on the approved wireframe.
 */
export default function CatalogueFilters({ categories, value, onChange }) {
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
        <input
          type="range"
          min={0}
          max={MAX_COST_CAP}
          step={5}
          value={value.maxUnitCost}
          aria-label="Maximum unit cost"
          onChange={(e) => onChange({ ...value, maxUnitCost: Number(e.target.value) })}
          className="w-full accent-brand-700"
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
