/**
 * The one-line computed insight shown at the top of every report tab (built by
 * lib/insights.js from the ReportInsightDto the tab's endpoint returned, or — for the
 * snapshot-only Inventory Valuation tab — from the rows already on screen). Purely
 * presentational; renders nothing when there's no sentence yet (still loading).
 */
export default function ReportInsight({ text }) {
  if (!text) return null

  return (
    <p className="mb-4 rounded-md border border-brand-100 bg-brand-50 px-4 py-2.5 text-sm text-brand-900">
      {text}
    </p>
  )
}
