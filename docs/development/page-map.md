# Page Map

> Synchronised with `origin/main` @ **95b4553**, 2026-08-24.
>
> Routes are now **real** — taken from [the Plan](../../__ai_agents/Stationery_Management_System_Project_Plan.md)
> §4.2 (the ~45-endpoint catalogue). Do not invent routes; look them up there.
> Owners are the Plan's **M1–M5** labels (§6.1). **Which person is M1–M5 is NOT SPECIFIED** —
> the Plan never maps a label to a name.
>
> **Status is `Not started` for every page.** No application code exists (verified 2026-08-24).

Base path for all routes: `/api/v1`. Auth column: *Any* = any authenticated user;
*Manager+* = `RankLevel ≥ 2`.

---

## Summary

| # | Page | Role(s) | Owner | Milestone | Wireframe | Priority |
|---|---|---|---|---|---|---|
| 1 | Login | anonymous | M1 (+M2 UI) | M1 | ✗ | P0 |
| 2 | Change Password | all | M1 | M1 | ✗ | P0 |
| 3 | Dashboard | all | — **not in the Plan** | — | ✓ | see note |
| 4 | Catalogue | Requestor | M2 | M2 | ✓ | P0 |
| 5 | New Request | Requestor | M4 (+M2 UI) | M3 | ✓ | P0 |
| 6 | My Requests | Requestor | M4 (+M2 UI) | M3 | ✗ | P0 |
| 7 | Approvals Queue | Approver | M4 (+M2 UI) | M4 | ✓ | P0 |
| 8 | Request Review | Approver | M4 | M4 | ✗ | P0 |
| 9 | Reports | Manager+ | M3 (SQL) + M2 (UI) | M5 | ✗ | P0 |
| 10 | Inventory / Stock Ledger | Manager+ | M3 | M2 | ✓ | P1 |
| 11 | Suppliers | Manager+ | M2 | M2 | ✗ | P1 |
| 12 | User Management | Manager+ | M1 | M1 | ✗ | P1 |
| 13 | Notifications | all | M4 (+M2 UI) | M4 | ✗ (bell) | P0 |
| 14 | My Eligibility | Requestor | M1 | M1 | ✗ | P0 |
| 15 | Help / Q&A | all | M4 (track T6.1) | M6 | ✗ | P0 |
| 16 | AI Request Assistant (A1) | Requestor | M5 | M5 | ✓ (panel) | **P0 — graded** |
| 17 | AI Shortage Forecast (A2) | Manager+ | M5 | M5 | ✗ | P1 |
| 18 | AI Supplier Recommendation (A3) | Manager+ | M5 | M5 | ✗ | P2 |
| 19 | AI Usage Report export | Manager+ | M5 | M5 | ✗ | P0 `[RUBRIC]` |
| 20 | Health endpoint | anonymous | M5 | M0 | — | P0 |

---

## 1–2. Login · Change Password — M1

| Method | Route | Auth |
|---|---|---|
| POST | `/auth/login` | Anonymous |
| POST | `/auth/change-password` | Any |
| GET | `/auth/me` | Any |

Employee number + password → JWT (HS256, 8h, `sub` = EmployeeNumber) + profile. `/auth/me`
returns role, rank, superior and whether the user is an approver. Passwords via
`PasswordHasher<T>` (PBKDF2, 100k iterations); invalid login → **401 with a generic message**.

**Change password fires a notification to the user *and* their superior** — trigger 6 of 6, and
the one most often forgotten (TC-14). It lives in `AuthService`, outside the request lifecycle.

Entities: `Users`, `Roles`, `Notifications`.
**Self-registration is not in the Plan** — employees are created by Manager+ via `/users`.

## 3. Dashboard — ⚠️ not in the Plan

`Dashboard.png` exists and the wireframe nav lists it, but **the Plan's endpoint catalogue has no
dashboard endpoint and no milestone owns it.** Its tiles map onto endpoints that do exist —
pending-approval count, `/inventory/low-stock`, `/users/me/eligibility`, `/requests/mine` — so it
is composable without new backend work. Treat as **NOT SPECIFIED**: confirm ownership and whether
it is in scope before building it.

Wireframe: KPI cards (Pending Approvals · Low Stock Alerts · Remaining Budget "62% of monthly
allocation") · Recent Requests table · Low Stock panel with **Reorder** buttons.
⚠️ *Reorder* has no endpoint; the nearest real action is `POST /inventory/{itemId}/receive`.
⚠️ `SKU` shown here is a Plan **future improvement** — don't build it.

## 4. Catalogue — M2 (M2 milestone)

| Method | Route | Auth |
|---|---|---|
| GET | `/items` | Any — **role-filtered** |
| GET | `/items/{id}` | Any |
| POST · PUT | `/items` · `/items/{id}` | Manager+ |
| PATCH | `/items/{id}/status` | Manager+ (deactivate, never delete) |
| GET | `/categories` | Any |

Role-based availability `[SPEC]` is implemented as `StationeryItems.MinRankLevelToRequest` vs the
caller's `Roles.RankLevel` `[DECISION — Plan §M2]` — an Engineer (rank 1) cannot request an item
flagged rank 3+. This is Plan `[ASK]` #3; if the instructor means something else it is one
`WHERE` clause. The wireframe's *"Available to Me"* filter is this rule.

Deactivating an item hides it from the catalogue but **preserves it in historical requests**.

Entities: `StationeryItems`, `Categories`, `Suppliers`, `Roles`.
⚠️ `Notify Me` (out-of-stock subscription) and the `MGR APPROVAL REQ` badge appear in the
wireframe with **no entity, endpoint or Plan concept** behind them — NOT SPECIFIED.

## 5. New Request — M4 backend, M2 UI (M3 milestone)

| Method | Route | Auth |
|---|---|---|
| POST | `/requests` | Any (create draft) |
| PUT | `/requests/{id}` | Owner — edit while `Draft` (⚠️ *or `ReturnedForModification`* — see K1) |
| POST | `/requests/{id}/submit` | Owner |
| POST | `/ai/request-assistant` | Any (see page 16) |

Server-side guards on submit — **all mandatory**:
- ≥ 1 line item; `RequiredByDate ≥ today` → else **400**
- total ≤ role threshold → else **422, naming the limit and the overage**
- **the server snapshots `UnitCostSnapshot` per line and computes the total** — never the client
- superior resolved from the hierarchy; **the MD (no superior) cannot raise a request**
  (Plan `[ASK]` #11 default)

**`[DECISION — Plan §M3]`** The spec asks the user to type their superior's email; we **pre-fill
it from the hierarchy and make it read-only**. Free-typing an arbitrary email is an
authorisation bypass. Document this deviation in the CRS.

One transaction on success: `Status = Pending` → set `ApproverEmployeeNumber` →
`RequestStatusHistory` row → `NotifyBoth` (2 `Notifications` rows).

Entities: `Requests`, `RequestItems`, `StationeryItems`, `RoleThresholds`, `Users`,
`RequestStatusHistory`, `Notifications`.
⚠️ Wireframe shows **`Department`** (required) and **`Justification / Notes`** — neither exists in
the schema or the Plan. NOT SPECIFIED.

## 6. My Requests — M4 backend, M2 UI (M3 milestone)

| Method | Route | Auth |
|---|---|---|
| GET | `/requests/mine` | Any — filter by status, paged |
| GET | `/requests/{id}` | Owner or approver — detail + status history |
| POST | `/requests/{id}/withdraw` | Owner — **`Pending` only**, else 409 |
| POST | `/requests/{id}/request-cancellation` | Owner — **`Approved` only** → `CancellationPending` |

**Withdraw ≠ Cancel.** Withdraw is unilateral on a `Pending` request. Cancellation is two-step and
needs the superior's second sign-off on an `Approved` one. This is the requirement teams most
often collapse into a delete — **never `DELETE` a submitted request**. Requesting cancellation
does **not** touch stock; only the superior's approval does.

Another user's request → **404, not 403**.

## 7–8. Approvals Queue · Request Review — M4 (M4 milestone)

| Method | Route | Auth |
|---|---|---|
| GET | `/requests/pending-approval` | Approver — only where the caller is the listed approver |
| POST | `/requests/{id}/approve` | Approver |
| POST | `/requests/{id}/reject` | Approver — **comment required**, else 400 |
| POST | `/requests/{id}/return` | Approver — comment required ⚠️ **contested, see K1** |
| POST | `/requests/{id}/cancellation-decision` | Approver — approve or refuse |

**Approve is a transaction, not an update** (Plan §M4, the highest-risk milestone). Atomically:
validate stock on every line → decrement `QuantityAvailable` → write `Issue` ledger rows → set
status/`DecidedAtUtc`/approver → write history → insert two notifications. Insufficient stock →
**422 with nothing written**. Stale `RowVersion` → **409**. A failure on line 3 must leave lines
1–2 untouched (**TC-08**).

**Cancellation approval restores stock via `Adjustment` ledger rows** — append, never edit or
delete the original `Issue` rows. Refusal returns the request to `Approved`, stock untouched.

⚠️ **K1:** whether `/return` exists at all is contested. The Plan, `frontend.md` and the M4
acceptance criteria include it; `docs/Diagrams/request_diagrams_v3.drawio` declares it out of
scope and says the Plan must be updated. **Ask before implementing the status enum.**

## 9. Reports — M3 (SQL) + M2 (UI), M5 milestone

| Method | Route | Auth |
|---|---|---|
| GET | `/reports/cost-by-item` | Manager+ |
| GET | `/reports/item-headcount` | Manager+ |
| GET | `/reports/cumulative-cost` | Manager+ |

All three take `?fromDate=&toDate=` and count **`Approved` requests only** (Plan `[ASK]` #6
default: money is committed at approval). **Three distinct views, not one page with three
columns.**

Rules that carry marks: **SQL-side `GROUP BY`** — never `ToList()` then LINQ-in-memory, proven via
the EF query log · percentages **sum to exactly 100.00%**, computed as `100 − sum(others)` ·
headcount is **distinct requestors** (TC-17) · unit cost comes from `UnitCostSnapshot`, never the
live catalogue · under 2 seconds on the seeded dataset · Engineer → **403**.

## 10. Inventory / Stock Ledger — M3 (M2 milestone)

| Method | Route | Auth |
|---|---|---|
| GET | `/inventory` | Manager+ |
| POST | `/inventory/{itemId}/adjust` | Manager+ — **reason required** → ledger row |
| POST | `/inventory/{itemId}/receive` | Manager+ — goods receipt → ledger row |
| GET | `/inventory/{itemId}/transactions` | Manager+ |
| GET | `/inventory/low-stock` | Manager+ |

`QuantityAvailable` may **only** be written through `IStockService` — verified by code review; no
other service touches that column. Balance must always equal `SUM(ChangeQuantity)` (asserted by
test). The wireframe's *Adjust Stock* / *Receive Goods* buttons map to the two POST routes.

⚠️ `SKU` in the wireframe is a Plan **future improvement** — do not build it.

## 11. Suppliers — M2 (M2 milestone)

`GET / POST / PUT /suppliers[/{id}]` — Manager+. Deactivate, never delete; **cannot deactivate a
supplier that still has active items → 409**. `LeadTimeDays` feeds the AI reorder maths (page 17).
No wireframe.

## 12. User Management — M1 (M1 milestone)

| Method | Route | Auth |
|---|---|---|
| GET · POST | `/users` | Manager+ |
| PUT | `/users/{empNo}` | Manager+ — **must reject hierarchy cycles** |
| PATCH | `/users/{empNo}/status` | Manager+ |
| GET | `/users/{empNo}/subordinates` | Any (self or Manager+) |

Validates the `[SPEC]` field rules: name ≤ 15 chars with no underscores, email ≤ 25 chars and
unique, employee number 1–1000. **Cycle detection** walks the superior chain to depth 10 →
**400** (TC-20). The MD has `SuperiorEmployeeNumber = NULL` and must load without a
null-reference (TC-19).

⚠️ **K2:** `StationerySchema.sql` currently contradicts all three field rules
(`IDENTITY(1,1)` with no range check, `NVARCHAR(200)`, `NVARCHAR(256)`). The Plan's `[SPEC]`
versions win — the SQL needs fixing. No wireframe.

## 13. Notifications — M4 (M4 milestone)

| Method | Route | Auth |
|---|---|---|
| GET | `/notifications` | Any — paged feed |
| GET | `/notifications/unread-count` | Any — **polled every 30s**, must be a single indexed COUNT |
| POST | `/notifications/{id}/read` · `/notifications/read-all` | Owner / Any |

**`[DECISION]`** In-app only: a persisted `Notifications` row + polled bell counter + toast.
**Email/SMTP and SignalR are `[CUT]`.** Polling pauses on `document.hidden`.

`INotificationService.NotifyAsync(eventType, request, actor)` is called **inside the same
transaction** as the state change and writes **two rows — the actor and their superior**.
Six triggers `[SPEC]`: request entered · cancelled · withdrawn · approved · rejected ·
**password changed**. TC-13 is a 6-case parameterised test asserting 2 rows each.

Surfaced as the bell + badge in every wireframe's top bar; no dedicated wireframe.

## 14. My Eligibility — M1 (M1 milestone)

`GET /users/me/eligibility` — Any. Returns role, limit, month-to-date spend and remaining `[SPEC]`.
Thresholds are **per role**, from `RoleThresholds` (`MaxAmountPerRequest`, `MaxAmountPerMonth`).
Seed values: Engineer 500 / Manager 2,000 / Business Manager 5,000 / MD 20,000; currency defaults
to **VND** (Plan `[ASK]` #10) — note the wireframes show `$`.

Over-threshold submission is a **hard block (422)** behind config flag
`EligibilityMode = Block|Warn` (Plan `[ASK]` #6). No dedicated wireframe — the Dashboard's
*Remaining Budget* card is the only place this currently appears.

## 15. Help / Q&A — track T6.1 (M6 milestone)

`GET /help/faq` — Any. **≥ 15 questions covering every feature** in the endpoint catalogue `[SPEC]`:
login, password change, availability, requesting, eligibility, approving, withdrawing,
cancelling, notifications, reports, and the AI assistant.

⚠️ Minor inconsistency: the Plan lists the endpoint under *"System — Member 5"* (§4.2) but assigns
track T6.1 to **M4** (§7 M6). Confirm the owner. Content does not exist yet and is graded.

## 16–19. AI features — M5 (M5 milestone)

| Method | Route | Auth | Priority |
|---|---|---|---|
| POST | `/ai/request-assistant` | Any | **P0 — the graded feature (A1)** |
| GET | `/ai/shortage-forecast` | Manager+ | P1 (A2) |
| GET | `/ai/supplier-recommendation/{itemId}` | Manager+ | P2 (A3) |
| GET | `/ai/usage-report` | Manager+ | P0 `[RUBRIC]` |

**Hard sequencing rule: A1 must pass acceptance before A2 starts.** A polished single feature
beats three half-features.

**A1 — Request Assistant** *(the wireframe's right-hand panel on New Request)*. Natural language →
a **validated, editable draft** that the user reviews and submits. Non-negotiable rules (Plan §5.2):
the **LLM never writes to the database**; the API key is server-side only; **user text is a `user`
message, never concatenated into the system prompt**; model-returned item IDs not in the loaded
catalogue are discarded silently; 10s timeout + one retry, then a keyword-matching fallback —
**the demo must work with the network unplugged**; rate limit 20 calls/user/hour; every call
logged to `AiInteractionLogs`.

**A2 — Shortage forecast.** The maths is **deterministic and unit-tested independently of the LLM**:
`ADC = SUM(|Issue|, 60d)/60` · `LeadTimeDemand = ADC × LeadTimeDays` · `SafetyStock = ADC × 3` ·
`ReorderPoint = LeadTimeDemand + SafetyStock` · status `REORDER NOW` / `WATCH` (<14 days) / `OK`.
The LLM only turns the table into a paragraph. **Do not call this machine learning.**

**A3 — Supplier recommendation (P2).** Weighted scoring (cost 40 / lead time 40 / reliability 20),
LLM writes the rationale.

**Usage report export** reads `AiInteractionLogs` — this table *is* the `[RUBRIC]` evidence.
Distinct from the hand-maintained developer log at root `AI_usage_report.md`.

LLM provider down → **503**; the AI feature is behind config `Features:AiAssistant`, so disabling
it is a config change, not a revert.

## 20. Health — M5 (M0 milestone)

`GET /health` — Anonymous. Liveness + DB connectivity for Docker/Jenkins:
`{"status":"Healthy","database":"Connected"}`. Part of the M0 walking skeleton.

---

## Explicitly out of scope — `[CUT]` (Plan §1.3)

Building any of these is a **scope breach**; escalate to the Project Leader:
email/SMTP notifications · SignalR real-time push · refresh-token rotation · microservices ·
a trained ML model · file uploads / item images · multi-language UI · dark mode · Redis caching ·
Kubernetes · payment or **procurement PO generation** · mobile app.

Also absent from every document: customer-facing product browsing, cart/checkout, delivery
tracking, and a separate admin panel (this is one application with role-varying permissions).
