# Role Spending Eligibility ("Remaining Budget") — Implementation Handoff

Phase 1 of the Plan's eligibility feature: the **read model** + the dashboard's "Remaining
Budget" tile. Submit-time enforcement (422 over-limit — Plan §M3 / TC-05) is **Phase 2, not
built** (it edits the M4-owned request-submit flow — coordinate first).

## What it is

A per-**role** monthly spending allowance, consumed per-**employee**. Every Engineer has
their own $500/month; a Manager $2,000; Business Manager $5,000; Managing Director $20,000.
"Remaining budget" = that limit − what this employee has already committed this calendar
month. It behaves like a hold/reservation ledger: a withdrawn or rejected request frees its
amount back up.

## Endpoint

`GET /api/v1/users/me/eligibility` — `[Authorize]` (any authenticated user; every role has a
limit). Added to `UsersController`.

```
EligibilityDto {
  role: string,               // "" if the user somehow has no role
  rankLevel: int,
  maxAmountPerRequest: decimal,
  maxAmountPerMonth: decimal,
  monthToDateSpend: decimal,
  remainingThisMonth: decimal, // max(0, maxAmountPerMonth - monthToDateSpend)
  monthResetsOn: "YYYY-MM-DD"  // 1st of next UTC month
}
```

`Infrastructure/Queries/EligibilityQueries.cs` resolves the role via
`AspNetUserRoles → AspNetRoles` (same path `ReportQueries` uses for rank), then sums the
employee's own `Requests`.

## Decisions made (all flagged, all reversible)

| Decision | Choice | Why / alternative |
|---|---|---|
| **Where thresholds live** | Two columns on `ApplicationRole` (`AspNetRoles`) | Consistent with the Identity fold that already put `RankLevel` there (CLAUDE.md K8). The schema of record (`StationerySchema.sql`) models them as a separate `RoleThresholds` table 1:1 with `Roles` — **needs an ERD/SQL reconciliation note, K-list style**. |
| **`MaxAmountPerRequest` value** | = `MaxAmountPerMonth` (one request may use the whole month) | The schema has the column; no number is documented anywhere. Tighten in `DbSeeder.Roles` if the team wants a stricter single-request cap. |
| **Which requests count toward MTD** | Status ∈ {Pending, Approved, PartiallyApproved, Fulfilled, CancellationPending} — i.e. *not* Rejected / Withdrawn / Cancelled | A budget guardrail should count money the moment it's spoken for. Alt: Approved-only (matches the Reports feature's "committed spend"). |
| **Which date** | `Request.CreatedAtUtc` in the current UTC month | It's the allowance for *raising* requests this month. Reports use `DecidedAtUtc` — different intent. |
| **PartiallyApproved amount** | `TotalEstimatedCost` (the whole request) | There is no per-line approved-amount field. |
| **Currency** | Magnitudes only (500 / 2 000 / 5 000 / 20 000) | Plan `[ASK] #10` (VND vs $) unresolved; `formatCurrency` already stubs `$`. |

## Seeding

`DbSeeder.SeedRolesAsync` is now **create-or-update**: it also backfills `RankLevel` /
`MaxAmountPerRequest` / `MaxAmountPerMonth` on roles that already exist, so a DB created
before the columns existed gets the allowances on next startup. Values in `DbSeeder.Roles`.

## Migration

`20260903044750_AddRoleBudgetThresholds` — adds `MaxAmountPerMonth`, `MaxAmountPerRequest`
(`decimal(18,2) NOT NULL DEFAULT 0`) to `AspNetRoles`. Existing rows get 0; the seeder then
backfills.

## Frontend

- `api/users.js` → `getMyEligibility()`.
- `DashboardPage.jsx` → added to the dashboard's `Promise.all`, **caught individually**
  (`.catch(() => null)`) so an eligibility hiccup can't blank the whole dashboard.
- `DashboardKpis.jsx` → the "Remaining Budget" tile now shows
  `formatCurrency(remainingThisMonth)` + `"{pct}% of your ${monthly} monthly allowance"`
  (matches the wireframe's "62% of monthly allocation"), red when < 10% left, and falls back
  to the "—" placeholder when the call failed.

## Files

**New:** `Application/DTOs/Users/EligibilityDto.cs`,
`Application/Interfaces/Users/IEligibilityQueries.cs`,
`Infrastructure/Queries/EligibilityQueries.cs`,
`Infrastructure/Data/Migrations/20260903044750_AddRoleBudgetThresholds*.cs`,
`Tests/WebApi.IntegrationTests/EligibilityTests.cs`,
`docs/development/eligibility-budget.md`.

**Modified (shared surfaces — additive):** `Infrastructure/Identity/ApplicationRole.cs`,
`Infrastructure/Data/Configurations/ApplicationRoleConfiguration.cs`,
`Infrastructure/Data/DbSeeder.cs`, `Infrastructure/Data/Migrations/DataContextModelSnapshot.cs`,
`WebApi/Program.cs` (DI), `WebApi/Controllers/UsersController.cs`,
`frontend/src/api/users.js`, `frontend/src/pages/dashboard/DashboardPage.jsx`,
`frontend/src/pages/dashboard/components/DashboardKpis.jsx`.

## Tests actually run

- `dotnet build Project.slnx` — 0 errors.
- `dotnet test Project.slnx` — **91 passed** (26 unit + 65 integration, incl. the new
  4-test `EligibilityTests`: Engineer full allowance, Manager higher allowance, MTD counts
  only committed-this-month requests, remaining clamped at 0 over-limit).
- `npm run build` (1709 modules) + `npm test` (91) — pass.
- **Not done:** browser click-through of the tile by the developer; live smoke test against
  a running backend (build + tests only).

## Reviewer follow-ups

1. Confirm the schema-vs-code divergence (columns on `AspNetRoles` vs a `RoleThresholds`
   table) and add a K-list entry reconciling the ERD / `StationerySchema.sql`.
2. Confirm the MTD status set and `CreatedAtUtc` (vs `DecidedAtUtc`) choice.
3. Confirm `MaxAmountPerRequest == MaxAmountPerMonth` is acceptable, or set a real per-request cap.
4. **Phase 2** — submit-time enforcement in `RequestService.SubmitAsync` (per-request +
   monthly cap → 422, behind `Eligibility:Mode = Block|Warn`, TC-05 tests). Crosses into the
   M4-owned request lifecycle; needs a coordination call before implementing.

---

## Phase 2 — submit-time enforcement (2026-09-05, audit C7)

Phase 2 is now built. `RequestService.SubmitAsync` refuses an over-budget submission with
**HTTP 422** before the `Draft → Pending` transition.

**Rule:** `request.TotalEstimatedCost > eligibility.RemainingThisMonth` → refuse.

- Compared against **`RemainingThisMonth`**, not the raw monthly allowance — the threshold is a
  monthly budget, so what matters is what is left after the month's other commitments. (Team
  decision, 2026-09-05.) `MaxAmountPerRequest` is **not** enforced; it is currently seeded equal
  to the monthly figure, so it would be a no-op. Wire it in if a stricter single-request cap is
  ever set.
- **Hard block**, no `Eligibility:Mode` config knob. `[ASK] #6` is answered as the Plan's own
  stated default (§M3: "hard-block is the defensible default"). One code path to test and
  defend in review.
- **No double-count.** `EligibilityQueries.CommittedStatuses` starts at `Pending`; the request
  being submitted is still `Draft` at the moment of the check, so `RemainingThisMonth` excludes
  the very total being tested.
- The guard sits on **submit**, not create — Plan §3.6 puts it on the `Draft → Pending`
  transition. An over-budget basket saves as a draft; it just cannot be sent.

**New exception → status mapping.** `Application/Exceptions/BusinessRuleException.cs` maps to
**422 Unprocessable Entity** in `ExceptionHandlingMiddleware` (Plan §4.2 error table: "422 ·
Business rule violation · Request total exceeds role threshold"). This is the first 422 in the
codebase; C8's stock guard reuses it.

**Known edge — month attribution.** `EligibilityQueries` windows month-to-date on
`Request.CreatedAtUtc`. A draft created in January and submitted in February is therefore
judged against, and counted into, **January's** budget. Acceptable for this project's flow
(drafts are short-lived) but worth a team decision if drafts start living across month
boundaries; the alternative is to stamp `CreatedAtUtc` at submit instead of create.
