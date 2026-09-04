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
/// Loads entities via AsNoTracking + full includes for efficiency in read-only scenarios.
/// Maps to DTOs in-memory for consistency across database providers.
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

        // Fetch IDs for pagination
        var ids = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync();

        // Load full DTOs
        var items = new List<RequestDto>(ids.Count);
        foreach (var id in ids)
        {
            var dto = await GetByIdAsync(id, visibleToEmployeeNumber);
            if (dto is not null)
            {
                items.Add(dto);
            }
        }

        return new PagedResult<RequestDto>(items, page, pageSize, totalCount);
    }

    public async Task<RequestDto?> GetByIdAsync(int requestId, int visibleToEmployeeNumber)
    {
        var scope = await hierarchy.GetVisibleRequestorScopeAsync(visibleToEmployeeNumber);

        // Category and Supplier hang off StationeryItem, so the Items -> Item chain is repeated
        // for each leaf navigation. Without these the DTO's categoryName/supplierName were read
        // off unloaded references under AsNoTracking and were therefore always null, which the
        // UI rendered as its "General" / "Preferred Supplier" placeholders (audit finding C9).
        var request = await db.Requests
            .AsNoTracking()
            .Include(r => r.Items).ThenInclude(i => i.Item).ThenInclude(item => item!.Category)
            .Include(r => r.Items).ThenInclude(i => i.Item).ThenInclude(item => item!.Supplier)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null)
        {
            return null;
        }

        // Visibility: own request, one pending my approval, or one raised inside my sub-tree
        // (null scope == Managing Director).
        var canSee = request.RequestorEmployeeNumber == visibleToEmployeeNumber
            || request.ApproverEmployeeNumber == visibleToEmployeeNumber
            || scope is null
            || scope.Contains(request.RequestorEmployeeNumber);

        if (!canSee)
        {
            return null; // Return null instead of 404 to avoid leaking existence
        }

        // Map to DTO
        var requestorName = await db.Users
            .Where(u => u.Id == request.RequestorEmployeeNumber)
            .Select(u => u.Name)
            .FirstOrDefaultAsync();

        var approverName = request.ApproverEmployeeNumber.HasValue
            ? await db.Users
                .Where(u => u.Id == request.ApproverEmployeeNumber.Value)
                .Select(u => u.Name)
                .FirstOrDefaultAsync()
            : null;

        var itemDtos = request.Items
            .OrderBy(i => i.Id)
            .Select(i =>
            {
                var categoryName = i.Item?.Category?.Name;
                var supplierName = i.Item?.Supplier?.Name;

                return new RequestItemDto(
                    i.Id,
                    i.ItemId,
                    i.Item?.ItemName ?? string.Empty,
                    categoryName,
                    i.Item?.SupplierId,
                    supplierName,
                    i.Quantity,
                    i.UnitCostSnapshot,
                    i.LineTotal,
                    i.Decision,
                    i.ApprovedQuantity
                );
            })
            .ToList();

        // Awaited one at a time. The previous version was `.Select(async h => ... await ...)`
        // followed by `.ToList()`, which starts every actor-name query at once against the same
        // DbContext and throws "A second operation was started on this context instance" as soon
        // as a request has more than one history row — i.e. on every request after its first
        // status change, which broke approve/reject/withdraw entirely. DbContext is not
        // thread-safe, so these must not overlap.
        var orderedHistory = request.StatusHistory
            .OrderBy(h => h.CreatedAtUtc)
            .ThenBy(h => h.Id)
            .ToList();

        var historyList = new List<RequestStatusHistoryDto>(orderedHistory.Count);
        foreach (var h in orderedHistory)
        {
            var actorName = await db.Users
                .Where(u => u.Id == h.ActorEmployeeNumber)
                .Select(u => u.Name)
                .FirstOrDefaultAsync();

            historyList.Add(new RequestStatusHistoryDto(
                h.Id,
                h.RequestId,
                h.FromStatus,
                h.ToStatus,
                h.ActorEmployeeNumber,
                actorName,
                h.Comment,
                h.CreatedAtUtc
            ));
        }

        return new RequestDto(
            request.Id,
            request.RequestorEmployeeNumber,
            requestorName,
            request.ApproverEmployeeNumber,
            approverName,
            request.Status,
            request.TotalEstimatedCost,
            request.RequiredByDate,
            request.DecisionComment,
            request.CreatedAtUtc,
            request.DecidedAtUtc,
            request.RowVersion,
            itemDtos,
            historyList
        );
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

        var ids = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync();

        var items = new List<RequestDto>(ids.Count);
        foreach (var id in ids)
        {
            var dto = await GetByIdAsync(id, approverEmployeeNumber);
            if (dto is not null)
            {
                items.Add(dto);
            }
        }

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

        var ids = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync();

        var items = new List<RequestDto>(ids.Count);
        foreach (var id in ids)
        {
            var dto = await GetByIdAsync(id, visibleToEmployeeNumber);
            if (dto is not null)
            {
                items.Add(dto);
            }
        }

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
}

