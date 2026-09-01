import { formatCurrency } from './format.js'

/**
 * Turns a `ReportInsightDto` (Application/DTOs/Reports/ReportInsightDto.cs) into the
 * one-line, plain-language sentence shown at the top of a report tab. The server computes
 * the numbers (current vs previous equal-length window, top mover) from the same scoped
 * dataset the report itself queries; this only chooses the wording.
 */
function possessiveToLower(scopeLabel) {
  return scopeLabel.charAt(0).toLowerCase() + scopeLabel.slice(1)
}

export function buildInsightSentence(insight) {
  if (!insight || insight.kind === 'Empty') {
    return 'No approved spend in this period yet.'
  }

  if (insight.kind === 'Composition') {
    const { scopeLabel, driverLabel, driverSharePercent, distinctRequestors } = insight
    if (!driverLabel) {
      return 'No approved spend in this period yet.'
    }
    const requestorPart = distinctRequestors === 1 ? '1 requestor' : `${distinctRequestors} requestors`
    return `${driverLabel} accounts for ${driverSharePercent.toFixed(1)}% of ${possessiveToLower(scopeLabel)} spend across ${requestorPart}.`
  }

  // "PeriodDelta" — spend this window vs the immediately-preceding equal-length window.
  const { scopeLabel, changePercent, driverLabel, currentTotal } = insight

  if (changePercent == null) {
    return `${scopeLabel} spend this period is ${formatCurrency(currentTotal)} — no prior period to compare yet.`
  }

  if (changePercent === 0) {
    return `${scopeLabel} spend is flat versus the previous period.`
  }

  const direction = changePercent > 0 ? 'up' : 'down'
  const magnitude = Math.abs(changePercent).toFixed(1)
  const driverClause = driverLabel && changePercent > 0 ? `, mostly driven by ${driverLabel}` : ''
  return `${scopeLabel} spend is ${direction} ${magnitude}% versus the previous period${driverClause}.`
}

/**
 * The Inventory Valuation tab is a point-in-time snapshot with no backend ReportInsightDto
 * (it's not date-ranged) — build its one-liner from the inventory rows already on screen.
 */
export function buildInventoryInsightSentence({ totalValue, itemsInStock, itemsNeedingReorder }) {
  if (itemsInStock === 0) {
    return 'No items currently in stock.'
  }
  if (itemsNeedingReorder === 0) {
    return `${formatCurrency(totalValue)} tied up in stock across ${itemsInStock} items — none at or below reorder level.`
  }
  const itemWord = itemsNeedingReorder === 1 ? 'item is' : 'items are'
  return `${formatCurrency(totalValue)} tied up in stock; ${itemsNeedingReorder} ${itemWord} at or below reorder level.`
}
