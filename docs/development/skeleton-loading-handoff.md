# Skeleton loading — implementation handoff

**Date:** 2026-09-03 · **Branch:** `khang` · **Scope:** frontend only. No API, no DB, no business
logic, no design change.

Replaces the centred spinner (`LoadingState`) with layout-shaped placeholders on every view whose
shape is known before the data arrives.

---

## 1. Why

Plan §9.2 already requires loading / error / empty on every data-fetching view, and the app had
all three. What it did not have was a loading state that *reserved the space the content would
occupy* — the spinner is a small centred block, so every page jumped when the data landed. On
Inventory the jump was largest: the summary tiles and the toolbar were rendered only once
`!loading`, so three cards and a filter bar appeared out of nowhere below the header.

This changes how *loading* looks. It does not change the rule, the error states, the empty
states, the auth behaviour or any business logic.

---

## 2. Architecture

### Shared primitives — `frontend/src/components/ui/Skeleton.jsx`

| Export | Stands in for |
|---|---|
| `Skeleton` | one shimmering block; `className` sets its size |
| `SkeletonText` | a paragraph (last line short) |
| `SkeletonTable` | any of the app's tables |
| `SkeletonStatCards` | the `StatCard` row, or (with `kpi`) the richer Dashboard KPI tile |
| `SkeletonCardGrid` | the Catalogue product-card grid |
| `SkeletonList` | list-shaped content — a modal list, the notification feed |

Two page-specific compositions, built from those primitives, live next to their pages:
`pages/dashboard/components/DashboardSkeleton.jsx` and
`pages/reports/components/ReportSkeleton.jsx`.

### The two rules that keep a placeholder aligned

Both are documented in the file header, because getting them wrong is what makes a skeleton look
*almost* right and therefore worse than a spinner:

1. **Bar heights match the line box of the text they replace** — `text-xs → h-4`,
   `text-sm → h-5`, `text-lg → h-7`, `text-3xl → h-9`. A column entry can also declare `height`
   where the real cell holds something taller than text: `h-6` for a status badge, `h-8` for a
   row of `size="sm"` buttons. The tallest bar sets the row height, exactly as the real content
   does.
2. **Cell padding is passed in** (`cellClassName`), because the tables here are not all `px-4` —
   the Dashboard panel is `px-5` and the New Request picker `px-3`.

### `SkeletonTable` columns

`columns` is a count, or one entry per column:

```jsx
columns={[6, 6, 11, 8, 5, { width: 5, height: 'h-6' }, { width: 9, align: 'right', height: 'h-8' }]}
```

- `number` / `{ width }` — the column's share of the table width, relative to the others
- `{ align: 'right' }` — for numeric and action columns
- `{ bar }` — a narrower bar for a narrow column (a checkbox, an icon button)
- `{ height }` — see rule 1

The table is `table-fixed` with a `<colgroup>` so those shares are honoured exactly. **This is
load-bearing**: without it the browser sizes cells by their content, and because skeleton cells
hold fixed-width blocks rather than text, every column collapses towards the left instead of
spanning the table the way the loaded one does. That was the visible defect in the first cut of
this work.

The weights are per-table and approximate each column's real share (driven by the header label
for short columns like "Employee #", by the content for long ones like Email). Two of them were
measured against the real tables rather than guessed — see §5.

### Accessibility

Each composite renders `role="status" aria-busy="true"` with a `sr-only` label carrying the same
wording the spinner used ("Loading users…", "Loading pending approvals…"). Screen readers still
announce the wait, and the five existing tests that assert on that text keep passing unchanged.
The blocks themselves are `aria-hidden`.

---

## 3. Coverage

| Page / component | Placeholder |
|---|---|
| Dashboard | `DashboardSkeleton` — KPI row + recent-requests table + (Manager+) low-stock panel |
| Catalogue | `SkeletonCardGrid` (6 cards) + placeholder rows in the category filter panel |
| Inventory | summary tiles + toolbar + 7-column ledger table (tiles and toolbar previously did not render at all while loading) |
| Reports | summary tiles (date-range tabs only) + `ReportSkeleton` — chart band over a figures table |
| My Requests | 8-column table |
| Approvals | 6-column table |
| New Request | search + stock-filter row, then the bordered 5-column picker table |
| User Management | 7-column table |
| Item Management | 6-column table |
| Suppliers | 4-column table |
| Subordinates modal | `SkeletonList`, `py-2` rows |
| Notification bell feed | `SkeletonList`, `px-3 py-2.5` rows, 3 lines |

`LoadingState` is **deliberately kept** in `routes/ProtectedRoute.jsx` for session restore: that
is a whole-app gate with no known shape to mimic, and a full-page skeleton there would imply a
layout the user may never see (they may be bounced to `/login`).

Not touched: the placeholder page (`Help`), anything static, and every error / empty state.

---

## 4. Loading-state behaviour

Unchanged from before: `useAsync` sets `loading` true only while the promise is genuinely
pending. Each page renders exactly one of skeleton / error / empty / content — the existing
`{loading && …} {!loading && error && …}` guards were kept, so the skeleton and real content can
never be on screen together, and there is no new timer, no minimum display time and no new
failure mode.

`CatalogueFilters` gained one prop, `loading`, used only to render four placeholder rows in place
of the category checkbox list. No other component's API changed.

---

## 5. What was actually verified

- `npx vitest run --pool=threads` — **98/98 passed** (17 files). Five of those assert that the
  skeleton is what renders while loading, via the `sr-only` label.
  ⚠️ the default `npm test` (forks pool) still hangs on this machine — use `--pool=threads`.
- `npx vite build` — clean.
- **Geometry, measured in the browser** against the real components (dev API on `:5263`, Vite on
  `:5173`) using a throwaway Vite entry that rendered each skeleton directly above the component
  it replaces. That harness has been deleted; the numbers it produced:

  | | skeleton | real |
  |---|---|---|
  | `StatCard` height | 94px | 94px |
  | Catalogue `ItemCard` height | 312px | 311px |
  | User table row (badge + buttons) | 57px | 57px |
  | Dashboard row (badge) | 49px | 49–51px |
  | User table column shares | 12 / 12 / 22 / 16 / 10 / 10 / 18 % | 11.7 / 12.1 / 21.4 / 16.3 / 10.2 / 10.2 / 18.1 % |
  | Dashboard column shares | 12.5 / 20.8 / 25 / 25 / 16.7 % | 12.8 / 22.3 / 24 / 23.4 / 17.5 % |

  The User and Dashboard weights come from those measurements. The other tables' weights are
  reasoned from header label + typical content and are **not** measured — if one looks off with
  real data, adjust that page's `columns` array; nothing else needs to change.

- **Not verified by a signed-in browser walkthrough of every page by the AI** — the assistant does
  not enter passwords, so the page-by-page visual pass was done by the developer.

---

## 6. Known limits / follow-ups

- Exact column alignment is impossible in general: the real tables are auto-layout, so their
  column widths depend on the data on screen. The weights get within ~1–2 percentage points on
  the two tables that were measured; treat them as a good approximation, not a guarantee.
- `ReportSkeleton` uses a single `h-48` chart band. That matches `LineChart` (`h-48`) exactly and
  `DonutChart` (`h-44` + legend) closely; `BarChart` grows with its row count, so a long bar
  report will still shift a little.
- The Reports "insight" sentence renders nothing until the data lands, so there is a one-line
  shift there. Left alone rather than reserving space for a sentence that is sometimes absent.
- No new test file was added. The five existing loading assertions cover the swap; a dedicated
  test for `SkeletonTable`'s column maths would be reasonable if the primitive grows.

---

## 7. Demo delay — used during development, removed

To demonstrate the skeletons against a fast local API, a temporary 1-second delay was added as
**one** mechanism deliberately kept outside every page and service: a `src/lib/demoDelay.js`
module plus a marked response interceptor in `src/api/client.js`. Nothing else referenced it.

**Both were removed before this work was committed.** `src/api/client.js` is byte-identical to
its previous state (`git diff` on it is empty), and `grep -rn "demoDelay" frontend/src` returns
nothing. The only `setTimeout` left in `src/` is the pre-existing post-submit redirect in
`NewRequestPage.jsx:211`, which is unrelated and stays.

If the skeletons need demonstrating again, re-add that same single interceptor rather than
scattering delays through the pages — keeping it in one place was the point.
