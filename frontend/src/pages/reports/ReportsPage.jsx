import { useEffect, useMemo, useState } from 'react'

import PageHeader from '../../components/layout/PageHeader.jsx'
import Card from '../../components/ui/Card.jsx'
import Button from '../../components/ui/Button.jsx'
import StatCard from '../../components/ui/StatCard.jsx'
import { ErrorState, EmptyState } from '../../components/ui/StateBlock.jsx'
import { SkeletonStatCards } from '../../components/ui/Skeleton.jsx'
import useAsync from '../../hooks/useAsync.js'
import { useAuth } from '../../contexts/AuthContext.jsx'
import { formatCurrency, formatNumber } from '../../lib/format.js'
import { exportToCsv } from '../../lib/csvExport.js'
import { defaultReportBounds, resolveRangeFromPreset } from '../../lib/reports.js'
import { buildInsightSentence, buildInventoryInsightSentence } from '../../lib/insights.js'
import {
  getCostByItemReport,
  getItemHeadcountReport,
  getCumulativeCostReport,
  getTeamExpenditureReport,
  getMyActivityReport,
} from '../../api/reports.js'
import { getInventory } from '../../api/inventory.js'
import {
  DEFAULT_REPORT_FILTERS,
  isDefaultReportFilters,
  applyReportFilters,
  categoryOptions,
} from './reportFilters.js'

import ReportTabs from './components/ReportTabs.jsx'
import DateRangeControl from './components/DateRangeControl.jsx'
import ReportToolbar from './components/ReportToolbar.jsx'
import ReportMetaBar from './components/ReportMetaBar.jsx'
import ReportInsight from './components/ReportInsight.jsx'
import MyActivityView from './components/MyActivityView.jsx'
import CostByItemView from './components/CostByItemView.jsx'
import ItemHeadcountView from './components/ItemHeadcountView.jsx'
import CumulativeCostView from './components/CumulativeCostView.jsx'
import InventoryValuationView from './components/InventoryValuationView.jsx'
import TeamExpenditureView from './components/TeamExpenditureView.jsx'
import ReportSkeleton from './components/ReportSkeleton.jsx'

const DEFAULT_PRESET_DAYS = 90

/**
 * Tab visibility by role (page-map §9 + the 2026-08-30 role-scoping change):
 * "My Requests" is always on — a manager who is also a requestor needs their own
 * personal spend separate from their team's. Inventory Valuation stays Manager+
 * (unchanged — it calls the existing Manager+ `/inventory` endpoint). By Team only
 * means something once there's more than one team to compare (Business Manager+);
 * a plain Manager has exactly one team, so it's hidden for them.
 *
 * This is UX only. The data itself is scoped server-side regardless of what tabs the
 * client shows (Infrastructure/Queries/ReportQueries.cs) — a requestor calling
 * /reports/by-team directly would get their own single-row scope back, not an error
 * and not someone else's data.
 */
const ALL_TABS = [
  { id: 'MY_REQUESTS', label: 'My Requests' },
  { id: 'COST_BY_ITEM', label: 'Cost by Item' },
  { id: 'HEADCOUNT', label: 'Cost & Headcount' },
  { id: 'CUMULATIVE', label: 'Cumulative Cost' },
  { id: 'INVENTORY_VALUATION', label: 'Inventory Valuation', minRankLevel: 2 },
  { id: 'BY_TEAM', label: 'By Team', minRankLevel: 3 },
]

/** Tabs whose data comes from a date-range report endpoint. INVENTORY_VALUATION is not one. */
const FETCHERS = {
  MY_REQUESTS: getMyActivityReport,
  COST_BY_ITEM: getCostByItemReport,
  HEADCOUNT: getItemHeadcountReport,
  CUMULATIVE: getCumulativeCostReport,
  BY_TEAM: getTeamExpenditureReport,
}

const ITEM_TABS = ['COST_BY_ITEM', 'HEADCOUNT']

/**
 * Print rules are injected as a `<style>` in the document head (see the effect below),
 * so `@media print` stays isolated from normal rendering and the sidebar / header —
 * which live in components outside this page's scope — are hidden structurally.
 */
const PRINT_CSS = `
@media print {
  body * { visibility: hidden !important; }
  [data-print-region], [data-print-region] * { visibility: visible !important; }
  [data-print-region] {
    position: absolute !important; left: 0 !important; top: 0 !important;
    width: 100% !important; margin: 0 !important; padding: 0 !important;
    background: #fff !important; color: #000 !important;
  }
  [data-print-hide] { display: none !important; }
  [data-print-region] table, [data-print-region] table * {
    color: #000 !important; background: #fff !important; border-color: #000 !important;
  }
  [data-print-footer] {
    display: block !important; margin-top: 24px; padding-top: 8px;
    border-top: 1px solid #000; text-align: center; font-size: 11px;
  }
}
[data-print-footer] { display: none; }
`

const PrinterIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M6 9V2h12v7" /><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" /><rect x="6" y="14" width="12" height="8" rx="1" />
  </svg>
)

const DownloadIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><polyline points="7 10 12 15 17 10" /><line x1="12" y1="15" x2="12" y2="3" />
  </svg>
)

/** Tab-specific KPI tiles, computed from the report payload for the active tab. */
function statsFor(tabId, report) {
  if (tabId === 'MY_REQUESTS') {
    return [
      { label: 'Approved Spend', value: formatCurrency(report.approvedCost) },
      { label: 'Requests', value: formatNumber(report.requestCount) },
      { label: 'Items', value: formatNumber(report.itemCount) },
    ]
  }

  const spend = { label: 'Approved Spend', value: formatCurrency(report.totalApprovedCost) }

  if (tabId === 'COST_BY_ITEM') {
    return [
      spend,
      { label: 'Items', value: formatNumber(report.rows.length) },
      {
        label: 'Top Item Share',
        value: report.rows.length ? `${report.rows[0].percentOfTotal.toFixed(2)}%` : '—',
      },
    ]
  }

  if (tabId === 'HEADCOUNT') {
    const requests = report.rows.reduce((sum, row) => sum + row.requestCount, 0)
    return [
      spend,
      { label: 'Items', value: formatNumber(report.rows.length) },
      { label: 'Approved Requests', value: formatNumber(requests) },
    ]
  }

  if (tabId === 'BY_TEAM') {
    const requests = report.rows.reduce((sum, row) => sum + row.requestCount, 0)
    return [
      spend,
      { label: 'Teams', value: formatNumber(report.rows.length) },
      { label: 'Approved Requests', value: formatNumber(requests) },
    ]
  }

  const months = report.points.length
  return [
    spend,
    { label: 'Months', value: formatNumber(months) },
    {
      label: 'Avg / Month',
      value: months ? formatCurrency(report.totalApprovedCost / months) : '—',
    },
  ]
}

export default function ReportsPage() {
  const { user } = useAuth()
  const rankLevel = user?.rankLevel ?? 0
  const visibleTabs = useMemo(
    () => ALL_TABS.filter((t) => !t.minRankLevel || rankLevel >= t.minRankLevel),
    [rankLevel],
  )

  const [tab, setTab] = useState('COST_BY_ITEM')
  const [range, setRange] = useState(() => resolveRangeFromPreset(DEFAULT_PRESET_DAYS, defaultReportBounds()))
  const [filters, setFilters] = useState(DEFAULT_REPORT_FILTERS)
  const [generatedAt, setGeneratedAt] = useState(() => new Date())

  const bounds = useMemo(() => defaultReportBounds(), [])
  const todayIso = bounds.toDate
  const invActive = tab === 'INVENTORY_VALUATION'

  useEffect(() => {
    const style = document.createElement('style')
    style.id = 'reports-print-style'
    style.textContent = PRINT_CSS
    document.head.appendChild(style)
    return () => style.remove()
  }, [])

  // Date-range report tabs.
  const main = useAsync(() => {
    const fetcher = FETCHERS[tab]
    return fetcher ? fetcher(range) : Promise.resolve(null)
  }, [tab, range.fromDate, range.toDate])

  // Inventory valuation — only actually hits the API when its tab is active.
  const inv = useAsync(
    () => (invActive ? getInventory() : Promise.resolve(null)),
    [invActive],
  )

  const active = invActive ? inv : main
  const { error, loading, reload } = active

  // `main.data` briefly holds the previous tab's payload after a tab switch (before the
  // refetch resolves). Only treat it as this tab's data when the discriminator matches.
  const report = !invActive && main.data && main.data.kind === tab ? main.data : null
  const inventoryItems = invActive ? inv.data?.items ?? null : null
  const currentData = invActive ? inv.data : report

  useEffect(() => {
    if (currentData) setGeneratedAt(new Date())
  }, [currentData])

  const isItemTab = ITEM_TABS.includes(tab)

  const filteredRows = useMemo(() => {
    if (!report || !isItemTab) return []
    return applyReportFilters(report.rows, filters)
  }, [report, isItemTab, filters])

  const stats = useMemo(
    () => (!invActive && report ? statsFor(tab, report) : []),
    [invActive, tab, report],
  )
  const categories = useMemo(
    () => (report && isItemTab ? categoryOptions(report.rows) : []),
    [report, isItemTab],
  )

  const insightText = useMemo(() => {
    if (invActive) {
      if (!inventoryItems) return null
      const totalValue = inventoryItems.reduce((sum, it) => sum + it.quantityAvailable * it.unitCost, 0)
      const itemsInStock = inventoryItems.filter((it) => it.quantityAvailable > 0).length
      const itemsNeedingReorder = inventoryItems.filter((it) => it.status === 'REORDER_NOW').length
      return buildInventoryInsightSentence({ totalValue, itemsInStock, itemsNeedingReorder })
    }
    return report ? buildInsightSentence(report.insight) : null
  }, [invActive, inventoryItems, report])

  const loadingReport = loading || !currentData
  const reportEmpty =
    !loadingReport &&
    !error &&
    (invActive
      ? inventoryItems.length === 0
      : tab === 'CUMULATIVE'
        ? report.points.length === 0
        : report.rows.length === 0)
  const filteredEmpty =
    !loadingReport && !error && isItemTab && !reportEmpty && filteredRows.length === 0
  const filtersActive = !isDefaultReportFilters(filters)

  const canExport = !loadingReport && !error && !reportEmpty && !filteredEmpty

  function handleExport() {
    if (!canExport) return
    const { fromDate, toDate } = range

    if (tab === 'MY_REQUESTS') {
      return exportToCsv(
        `my-requests-${fromDate}-${toDate}.csv`,
        ['Item', 'Category', 'Approved Cost', 'Units'],
        report.rows.map((r) => [r.itemName, r.categoryName, formatCurrency(r.approvedCost), String(r.unitsApproved)]),
      )
    }
    if (tab === 'COST_BY_ITEM') {
      return exportToCsv(
        `cost-by-item-${fromDate}-${toDate}.csv`,
        ['Item', 'Category', 'Approved Cost', '% of Total'],
        filteredRows.map((r) => [
          r.itemName,
          r.categoryName,
          formatCurrency(r.approvedCost),
          `${r.percentOfTotal.toFixed(2)}%`,
        ]),
      )
    }
    if (tab === 'HEADCOUNT') {
      return exportToCsv(
        `cost-headcount-${fromDate}-${toDate}.csv`,
        ['Item', 'Category', 'Approved Cost', 'Units Approved', 'Distinct Requestors', 'Requests'],
        filteredRows.map((r) => [
          r.itemName,
          r.categoryName,
          formatCurrency(r.approvedCost),
          String(r.unitsApproved),
          String(r.requestorCount),
          String(r.requestCount),
        ]),
      )
    }
    if (tab === 'CUMULATIVE') {
      return exportToCsv(
        `cumulative-cost-${fromDate}-${toDate}.csv`,
        ['Month', 'Monthly Cost', 'Cumulative Cost'],
        report.points.map((p) => [
          p.periodLabel,
          formatCurrency(p.periodCost),
          formatCurrency(p.cumulativeCost),
        ]),
      )
    }
    if (tab === 'BY_TEAM') {
      return exportToCsv(
        `cost-by-team-${fromDate}-${toDate}.csv`,
        ['Team (Manager)', 'Members', 'Requests', 'Approved Cost', '% of Total'],
        report.rows.map((r) => [
          r.teamName,
          String(r.memberCount),
          String(r.requestCount),
          formatCurrency(r.approvedCost),
          `${r.percentOfTotal.toFixed(2)}%`,
        ]),
      )
    }
    // INVENTORY_VALUATION — no Category column (see InventoryValuationView for why).
    return exportToCsv(
      `inventory-valuation-${todayIso}.csv`,
      ['Item', 'Qty Available', 'Unit Cost', 'Total Value', 'Status'],
      inventoryItems.map((it) => [
        it.itemName,
        String(it.quantityAvailable),
        formatCurrency(it.unitCost),
        formatCurrency(it.quantityAvailable * it.unitCost),
        it.status,
      ]),
    )
  }

  return (
    <div data-print-region>
      <PageHeader
        title="Reports"
        description="Approved stationery spend, headcount, trend, inventory value and team breakdown — scoped to what your role can see."
        actions={
          <div data-print-hide className="flex flex-wrap items-end gap-2">
            {invActive ? (
              <span className="text-sm text-ink-muted">Point-in-time snapshot</span>
            ) : (
              <DateRangeControl value={range} bounds={bounds} onChange={setRange} />
            )}
            <Button variant="secondary" onClick={() => window.print()}>
              <PrinterIcon />
              Print
            </Button>
            <Button
              variant="secondary"
              onClick={handleExport}
              disabled={!canExport}
              title={canExport ? undefined : 'No data to export'}
            >
              <DownloadIcon />
              Export CSV
            </Button>
          </div>
        }
      />

      <div data-print-hide className="mb-5">
        <ReportTabs tabs={visibleTabs} activeId={tab} onChange={setTab} />
      </div>

      <ReportMetaBar
        generatedAt={generatedAt}
        fromDate={range.fromDate}
        toDate={invActive ? todayIso : range.toDate}
        snapshot={invActive}
      />

      {/* The summary tiles only exist on the date-range tabs, so only reserve them there. */}
      {loadingReport && !error && !invActive && (
        <SkeletonStatCards
          label="Loading report summary…"
          count={3}
          grid="grid-cols-1 sm:grid-cols-3"
          className="mb-6"
        />
      )}

      {stats.length > 0 && (
        <div className="mb-6 grid grid-cols-1 gap-5 sm:grid-cols-3">
          {stats.map((stat) => (
            <StatCard key={stat.label} label={stat.label} value={stat.value} />
          ))}
        </div>
      )}

      <ReportInsight text={insightText} />

      {isItemTab && report && !reportEmpty && (
        <div data-print-hide>
          <ReportToolbar
            value={filters}
            categories={categories}
            onChange={setFilters}
            resultCount={filteredRows.length}
            totalCount={report.rows.length}
          />
        </div>
      )}

      <Card className="overflow-hidden">
        {loadingReport && !error && <ReportSkeleton />}

        {!loadingReport && error && <ErrorState error={error} onRetry={reload} />}

        {reportEmpty && (
          <EmptyState
            title={invActive ? 'No stock records' : 'No approved requests in this period'}
            description={
              invActive
                ? 'Once stationery items are stocked they will appear here.'
                : 'Widen the date range, or wait for requests in this window to be approved.'
            }
          />
        )}

        {filteredEmpty && (
          <EmptyState
            title="No items match your filters"
            description="Try a different search term, category or cost band."
            action={
              <Button variant="secondary" size="sm" onClick={() => setFilters(DEFAULT_REPORT_FILTERS)}>
                Clear filters
              </Button>
            }
          />
        )}

        {!loadingReport && !error && !reportEmpty && !filteredEmpty && (
          <>
            {tab === 'MY_REQUESTS' && <MyActivityView report={report} />}
            {tab === 'COST_BY_ITEM' && (
              <CostByItemView
                rows={filteredRows}
                totalApprovedCost={report.totalApprovedCost}
                filtered={filtersActive}
              />
            )}
            {tab === 'HEADCOUNT' && (
              <ItemHeadcountView
                rows={filteredRows}
                totalApprovedCost={report.totalApprovedCost}
                filtered={filtersActive}
              />
            )}
            {tab === 'CUMULATIVE' && <CumulativeCostView report={report} />}
            {tab === 'BY_TEAM' && <TeamExpenditureView report={report} />}
            {tab === 'INVENTORY_VALUATION' && <InventoryValuationView items={inventoryItems} />}
          </>
        )}
      </Card>

      <div data-print-footer>
        HMT Technologies Stationery Management System — Confidential
      </div>
    </div>
  )
}
