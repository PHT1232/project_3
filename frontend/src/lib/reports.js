/**
 * Small date-range helpers shared by the Reports page.
 *
 * The cost-by-item / headcount / cumulative / by-team aggregations used to live here as a
 * client-side stand-in for the backend (Plan §4.2/§5). They are now computed server-side,
 * scoped per user, in Infrastructure/Queries/ReportQueries.cs — see AI_usage_report.md for
 * the migration note. This file keeps only the date-range math the `DateRangeControl` needs.
 */

function isoMinusDays(iso, days) {
  const date = new Date(`${iso}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() - days)
  return date.toISOString().slice(0, 10)
}

function isoToday() {
  return new Date().toISOString().slice(0, 10)
}

/**
 * The date-range picker has no fixed data window to clamp to any more (that was a mock-data
 * artefact) — bound it to "today" and a generous two years back instead.
 */
export function defaultReportBounds() {
  const toDate = isoToday()
  return { fromDate: isoMinusDays(toDate, 730), toDate }
}

/**
 * Resolve a preset (in days back from today) to a concrete `{ fromDate, toDate }`, clamped to
 * `bounds`. `days == null` means "all time" (the full bounds).
 */
export function resolveRangeFromPreset(days, bounds) {
  if (days == null) return { fromDate: bounds.fromDate, toDate: bounds.toDate }
  const candidate = isoMinusDays(bounds.toDate, days)
  return {
    fromDate: candidate < bounds.fromDate ? bounds.fromDate : candidate,
    toDate: bounds.toDate,
  }
}
