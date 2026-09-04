using Application.DTOs.Common;
using Application.DTOs.Requests;
using Application.Interfaces.Requests;
using Application.Interfaces.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Query service for browsing and filtering stationery requests.
///
/// Row-level visibility follows the reporting line (CLAUDE.md #9, Plan §6, TC-15):
/// - Everyone sees their own requests, and any request pending their own approval.
/// - Manager / Business Manager additionally see every request raised by someone in their
///   reporting sub-tree (their reports, and their reports' reports, to any depth) — never a
///   peer's or a superior's.
/// - Managing Director sees all requests.
/// The sub-tree is resolved by <see cref="IHierarchyQueries"/>.
///
/// <b>Query shape.</b> Every list method resolves the caller's scope <i>once</i> and then loads
/// the whole page through <see cref="LoadPageAsync"/>: one query for the rows (with includes),
/// one for every display name the page needs, and no per-row work. It previously called
/// <see cref="GetByIdAsync"/> in a loop, which re-resolved the scope for every row — and scope
/// resolution loads the entire user adjacency table — then issued a further query per requestor,
/// per approver and per history row. A 100-row dashboard page cost ~100 full user-table scans
/// plus ~400 round trips (audit finding H1). It is now a fixed 3 queries per page.
///
/// Entities are read AsNoTracking and mapped to DTOs in memory, which keeps the projection
/// provider-portable across SQL Server and the SQLite test provider.
/// </summary>
public class RequestQueries(DataContext db, IHierarchyQueries hierarchy) : IRequestQueries
{
    public async Task<PagedResult<RequestDto>> GetVisibleAsync(
        int page,
        int pageSize,
        string? statusFilter,
        int visibleToEmployeeNumber)
    {
        var scope = await hierarchy.GetVisibleRequestorScopeAsync(visibleToEmployeeNumber);

        IQueryable<Core.Entities.Request> query = db.Requests.AsNoTracking();

        if (scope is not null) // null == Managing Director, no row filter
        {
            var scopedRequestorIds = scope.ToArray();
            query = query.Where(r =>
                scopedRequestorIds.Contains(r.RequestorEmployeeNumber)
                || r.ApproverEmployeeNumber == visibleToEmployeeNumber);
        }

        // Apply status filter if provided
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(r => r.Status == statusFilter);
        }

        var totalCount = await query.CountAsync();
        var ids = await PageIdsAsync(query, page, pageSize);
        var items = await LoadPageAsync(ids, scope, visibleToEmployeeNumber);

        return new PagedResult<RequestDto>(items, page, pageSize, totalCount);
    }

    public async Task<RequestDto?> GetByIdAsync(int requestId, int visibleToEmployeeNumber)
    {
        var scope = await hierarchy.GetVisibleRequestorScopeAsync(visibleToEmployeeNumber);
        var items = await LoadPageAsync([requestId], scope, visibleToEmployeeNumber);

        // Empty means "does not exist" or "not visible to this caller" — indistinguishable on
        // purpose, so the caller's 404 does not leak the existence of someone else's request
        // (CLAUDE.md principle #9).
        return items.Count == 0 ? null : items[0];
    }

    public async Task<PagedResult<RequestDto>> GetPendingApprovalsAsync(
        int page,
        int pageSize,
        int approverEmployeeNumber)
    {
        // Everything waiting on THIS approver's decision: Pending (approve / reject) and
        // CancellationPending (approve / refuse the cancellation — Plan §3.6). Filtering to
        // Pending alone left CancellationPending requests with no way to be found by the one
        // person who can resolve them (audit finding C5). Drafts never appear here.
        var query = db.Requests
            .AsNoTracking()
            .Where(r => (r.Status == "Pending" || r.Status == "CancellationPending")
                        && r.ApproverEmployeeNumber == approverEmployeeNumber);

        var totalCount = await query.CountAsync();
        var ids = await PageIdsAsync(query, page, pageSize);

        // Every row here is one this caller approves, so it passes the visibility rule by
        // construction — but the scope is still resolved and applied rather than bypassed, so
        // there is exactly one place that decides what a caller may see.
        var scope = await hierarchy.GetVisibleRequestorScopeAsync(approverEmployeeNumber);
        var items = await LoadPageAsync(ids, scope, approverEmployeeNumber);

        return new PagedResult<RequestDto>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<RequestDto>> GetByRequestorAsync(
        int requestorEmployeeNumber,
        int page,
        int pageSize,
        int visibleToEmployeeNumber,
        string? statusFilter = null)
    {
        // The caller may list a person's requests if it is their own list, or the person sits
        // inside the caller's reporting sub-tree (null scope == Managing Director).
        var scope = await hierarchy.GetVisibleRequestorScopeAsync(visibleToEmployeeNumber);

        if (visibleToEmployeeNumber != requestorEmployeeNumber
            && scope is not null
            && !scope.Contains(requestorEmployeeNumber))
        {
            return new PagedResult<RequestDto>([], page, pageSize, 0);
        }

        var query = db.Requests
            .AsNoTracking()
            .Where(r => r.RequestorEmployeeNumber == requestorEmployeeNumber);

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(r => r.Status == statusFilter);
        }

        var totalCount = await query.CountAsync();
        var ids = await PageIdsAsync(query, page, pageSize);
        var items = await LoadPageAsync(ids, scope, visibleToEmployeeNumber);

        return new PagedResult<RequestDto>(items, page, pageSize, totalCount);
    }

    public async Task<Dictionary<string, int>> GetStatusSummaryForDashboardAsync(int employeeNumber)
    {
        var scope = await hierarchy.GetVisibleRequestorScopeAsync(employeeNumber);

        IQueryable<Core.Entities.Request> query = db.Requests.AsNoTracking();

        if (scope is not null) // null == Managing Director, no row filter
        {
            var scopedRequestorIds = scope.ToArray();
            query = query.Where(r =>
                scopedRequestorIds.Contains(r.RequestorEmployeeNumber)
                || r.ApproverEmployeeNumber == employeeNumber);
        }

        // Count by status, excluding zero counts
        var summary = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .Where(x => x.Count > 0)
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        return summary;
    }

    // -------------------------------------------------------------------------------------
    // Shared loading. One place decides visibility; one place builds a RequestDto.
    // -------------------------------------------------------------------------------------

    /// <summary>Newest first, stable on ties. The ordering the returned page preserves.</summary>
    private static Task<List<int>> PageIdsAsync(IQueryable<Core.Entities.Request> query, int page, int pageSize) =>
        query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync();

    /// <summary>
    /// Loads and maps a whole page in a fixed number of queries: one for the requests, one for
    /// every display name they reference. Rows the caller may not see are dropped silently.
    /// The result keeps <paramref name="ids"/>' order, which is the page's sort order.
    /// </summary>
    private async Task<List<RequestDto>> LoadPageAsync(
        IReadOnlyList<int> ids, IReadOnlySet<int>? scope, int visibleToEmployeeNumber)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var idList = ids.ToList();

        // Category and Supplier hang off StationeryItem, so the Items -> Item chain is repeated
        // for each leaf navigation. Without these the DTO's categoryName/supplierName were read
        // off unloaded references under AsNoTracking and were therefore always null, which the
        // UI rendered as its "General" / "Preferred Supplier" placeholders (audit finding C9).
        var requests = await db.Requests
            .AsNoTracking()
            .Include(r => r.Items).ThenInclude(i => i.Item).ThenInclude(item => item!.Category)
            .Include(r => r.Items).ThenInclude(i => i.Item).ThenInclude(item => item!.Supplier)
            .Include(r => r.StatusHistory)
            .Where(r => idList.Contains(r.Id))
            .ToListAsync();

        var visible = requests
            .Where(r => CanSee(r, scope, visibleToEmployeeNumber))
            .ToList();

        if (visible.Count == 0)
        {
            return [];
        }

        var names = await LoadDisplayNamesAsync(visible);

        var byId = visible.ToDictionary(r => r.Id);

        // Re-apply the page's ordering: the IN(...) query above makes no ordering guarantee.
        return idList
            .Where(byId.ContainsKey)
            .Select(id => MapToDto(byId[id], names))
            .ToList();
    }

    /// <summary>
    /// Own request, one pending my approval, or one raised inside my reporting sub-tree.
    /// A null scope means Managing Director — no restriction.
    /// </summary>
    private static bool CanSee(Core.Entities.Request request, IReadOnlySet<int>? scope, int visibleToEmployeeNumber) =>
        request.RequestorEmployeeNumber == visibleToEmployeeNumber
        || request.ApproverEmployeeNumber == visibleToEmployeeNumber
        || scope is null
        || scope.Contains(request.RequestorEmployeeNumber);

    /// <summary>
    /// Every employee number the page's DTOs need a name for — requestors, approvers and history
    /// actors — fetched in one query. This replaces the previous per-row lookups, which also had
    /// to be awaited strictly one at a time because DbContext is not thread-safe.
    /// </summary>
    private async Task<Dictionary<int, string>> LoadDisplayNamesAsync(IReadOnlyCollection<Core.Entities.Request> requests)
    {
        var wanted = new HashSet<int>();

        foreach (var request in requests)
        {
            wanted.Add(request.RequestorEmployeeNumber);

            if (request.ApproverEmployeeNumber.HasValue)
            {
                wanted.Add(request.ApproverEmployeeNumber.Value);
            }

            foreach (var history in request.StatusHistory)
            {
                wanted.Add(history.ActorEmployeeNumber);
            }
        }

        var wantedList = wanted.ToList();

        return await db.Users
            .AsNoTracking()
            .Where(u => wantedList.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);
    }

    /// <summary>Pure mapping — no I/O, so it cannot reintroduce a per-row query.</summary>
    private static RequestDto MapToDto(Core.Entities.Request request, IReadOnlyDictionary<int, string> names)
    {
        var itemDtos = request.Items
            .OrderBy(i => i.Id)
            .Select(i => new RequestItemDto(
                i.Id,
                i.ItemId,
                i.Item?.ItemName ?? string.Empty,
                i.Item?.Category?.Name,
                i.Item?.SupplierId,
                i.Item?.Supplier?.Name,
                i.Quantity,
                i.UnitCostSnapshot,
                i.LineTotal,
                i.Decision,
                i.ApprovedQuantity))
            .ToList();

        var historyList = request.StatusHistory
            .OrderBy(h => h.CreatedAtUtc)
            .ThenBy(h => h.Id)
            .Select(h => new RequestStatusHistoryDto(
                h.Id,
                h.RequestId,
                h.FromStatus,
                h.ToStatus,
                h.ActorEmployeeNumber,
                Name(names, h.ActorEmployeeNumber),
                h.Comment,
                h.CreatedAtUtc))
            .ToList();

        return new RequestDto(
            request.Id,
            request.RequestorEmployeeNumber,
            Name(names, request.RequestorEmployeeNumber),
            request.ApproverEmployeeNumber,
            request.ApproverEmployeeNumber.HasValue ? Name(names, request.ApproverEmployeeNumber.Value) : null,
            request.Status,
            request.TotalEstimatedCost,
            request.RequiredByDate,
            request.DecisionComment,
            request.CreatedAtUtc,
            request.DecidedAtUtc,
            request.RowVersion,
            itemDtos,
            historyList);
    }

    /// <summary>Null when the employee no longer exists — same as the old per-row lookup.</summary>
    private static string? Name(IReadOnlyDictionary<int, string> names, int employeeNumber) =>
        names.TryGetValue(employeeNumber, out var name) ? name : null;
}
