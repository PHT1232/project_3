# Request Visibility — Reporting Sub-tree Scoping

## Why

`RequestQueries` scoped visible requests with a two-tier switch on
`ApplicationUser.RankLevel`:

| Rank | Saw |
|---|---|
| `< 2` | own requests + requests where they are the approver |
| `>= 2` | **every request in the system — no filter** |

So any Manager saw a Business Manager's requests, a Manager saw a peer Manager's requests,
and so on. Nothing in the Plan asks for that; the Plan asks for the opposite —
**TC-15 "cross-approver isolation"**, and §558 lists "approver acts on someone else's
subordinate" as a 403 case. The `GET /api/v1/requests` list endpoint itself is a team
addition (the Plan's request-GET catalogue has only `/mine` and `/pending-approval`).

Two extra problems with the old code:

- It read `ApplicationUser.RankLevel`, the **vestigial** field (CLAUDE.md K8). Rank of
  record comes from the assigned Identity role (`AspNetUserRoles → AspNetRoles.RankLevel`).
  Users created through `UserManager` (all of them, and every integration test) have
  `ApplicationUser.RankLevel == 0`, so the "Manager sees more" branch never actually ran
  under test.
- `GetByIdAsync`, `GetByRequestorAsync` and `GetStatusSummaryForDashboardAsync` each
  repeated the rule inline.

## The rule now (Plan §6, TC-15)

- Everyone sees **their own** requests and **any request pending their own approval**.
- **Manager / Business Manager** additionally see every request **raised by someone in their
  reporting sub-tree** — direct reports, their reports, to any depth — never a peer's or a
  superior's.
- **Managing Director** sees all requests.

## How

### `IHierarchyQueries` (Application) / `HierarchyQueries` (Infrastructure)

```csharp
Task<IReadOnlySet<int>?> GetVisibleRequestorScopeAsync(int actor, int maxDepth = 10);
```

- Rank via the role join (same as `ReportQueries` / `EligibilityQueries`).
- Rank ≥ 4 (Managing Director) → returns `null`, meaning "no row filter".
- Otherwise: load the `Id → SuperiorEmployeeNumber` adjacency for the whole user table once
  (an eProject has tens of users), expand breadth-first from `actor`, cap at `maxDepth`
  levels, de-duplicate visited nodes so a malformed hierarchy (a cycle slipping past the
  Plan §M1 T1.5 create/update guard) terminates instead of looping.

Kept in memory rather than a recursive CTE so it behaves identically on SQL Server and the
SQLite integration-test provider.

### `RequestQueries`

Constructor gains `IHierarchyQueries hierarchy`. Four methods now resolve
`scope = await hierarchy.GetVisibleRequestorScopeAsync(actor)` and filter:

```csharp
if (scope is not null)
{
    var ids = scope.ToArray();
    query = query.Where(r => ids.Contains(r.RequestorEmployeeNumber)
                          || r.ApproverEmployeeNumber == actor);
}
```

`GetByIdAsync` adds `|| scope is null || scope.Contains(request.RequestorEmployeeNumber)` to
its `canSee` check (still returns `null` → 404, never leaking existence).
`GetPendingApprovalsAsync` is untouched — it is already keyed on `ApproverEmployeeNumber`.

## Files

**New:** `Application/Interfaces/Users/IHierarchyQueries.cs`,
`Infrastructure/Queries/HierarchyQueries.cs`.

**Modified:** `Infrastructure/Queries/RequestQueries.cs`,
`Application/Interfaces/Requests/IRequestQueries.cs` (doc comments),
`WebApi/Program.cs` (DI), `Tests/WebApi.IntegrationTests/RequestsTests.cs`,
`frontend/src/api/requests.js` + `frontend/src/pages/dashboard/components/RecentRequestsCard.jsx`
(doc comments only — no frontend logic change; the dashboard's Recent Requests card just
renders whatever the now-correctly-scoped endpoint returns).

## Tests

`dotnet test Project.slnx` — **125 passed** (49 unit + 76 integration). New in `RequestsTests`,
over a `615 MD → 610 BM → {611 Mgr → 613 Eng, 612 Mgr → 614 Eng}` line:

- `GetVisible_Manager_SeesOwnSubordinatesRequests_ButNotAPeersOrSuperiors`
- `GetVisible_BusinessManager_SeesRequestsTwoLevelsDown`
- `GetById_Manager_CannotSeeAPeerManagersRequest_Returns404`
- `GetById_ManagingDirector_SeesAnyRequest`

`npx vitest run --pool=threads` — 98 passed.

## Decisions flagged for review

| Decision | Choice | Alternative |
|---|---|---|
| **Sub-tree vs direct reports** | Full sub-tree, any depth | The Plan's literal GET model is "owner or approver" only. The broader scope was explicitly requested ("…then further on"). Deviation is deliberate. |
| **Raised vs approved** | A Manager sees requests a subordinate **raised** | Could also include ones the subordinate *approves*; but approver == superior, so that set is already covered by the `ApproverEmployeeNumber == actor` clause for the subordinate, not the manager. |
| **Managing Director** | Unrestricted (`null` scope) fast path | Could walk the tree from the top like everyone else; the fast path mirrors `ReportScope.Org`. |
| **`maxDepth = 10`** | Matches the Plan §M1 cycle-walk cap | — |

## Reviewer follow-ups

1. Confirm the sub-tree scope is what the team wants for `GET /requests`, or tighten to the
   Plan's "owner or direct approver".
2. `ApplicationUser.RankLevel` is now unused by request visibility. K8 cleanup (drop the
   column, or backfill and keep) is still open and out of scope here.
3. `GetByRequestorAsync` / `GetStatusSummaryForDashboardAsync` no longer do an explicit
   "user exists" pre-check — an unknown employee number simply resolves to an empty scope and
   returns nothing. Behaviour is equivalent; noted in case a test elsewhere depended on the
   old `ApplicationException`.
