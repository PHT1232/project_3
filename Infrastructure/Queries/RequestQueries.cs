using Application.DTOs.Common;
using Application.DTOs.Requests;
using Application.Interfaces.Requests;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Query service for browsing and filtering stationery requests.
/// 
/// All queries enforce ownership/eligibility checks:
/// - Requestors see only their own requests (unless they are an approver for others).
/// - Approvers see requests they must approve (their subordinates' requests).
/// - Managers see all requests.
/// 
/// Loads entities via AsNoTracking + full includes for efficiency in read-only scenarios.
/// Maps to DTOs in-memory for consistency across database providers.
/// </summary>
public class RequestQueries(DataContext db) : IRequestQueries
{
    public async Task<PagedResult<RequestDto>> GetVisibleAsync(
        int page,
        int pageSize,
        string? statusFilter,
        int visibleToEmployeeNumber)
    {
        // Load the user to determine visibility scope
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == visibleToEmployeeNumber)
            ?? throw new ApplicationException($"User {visibleToEmployeeNumber} not found.");

        // Determine visibility: Manager+ sees all, others see their own + those they approve
        IQueryable<Core.Entities.Request> query = db.Requests.AsNoTracking();

        if (user.RankLevel < 2) // Engineer or below (not Manager or above)
        {
            query = query.Where(r =>
                r.RequestorEmployeeNumber == visibleToEmployeeNumber
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
        // Load the user for visibility check
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == visibleToEmployeeNumber);

        if (user is null)
        {
            return null;
        }

        // Load full request with navigation properties
        var request = await db.Requests
            .AsNoTracking()
            .Include(r => r.Items)
            .ThenInclude(i => i.Item)
            .Include(r => r.StatusHistory)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request is null)
        {
            return null;
        }

        // Visibility check: requestor, approver, or Manager+
        var canSee = request.RequestorEmployeeNumber == visibleToEmployeeNumber
            || request.ApproverEmployeeNumber == visibleToEmployeeNumber
            || user.RankLevel >= 2;

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
                    i.LineTotal
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
        // Get requests in Pending status where the caller is the approver
        var query = db.Requests
            .AsNoTracking()
            .Where(r => r.Status == "Pending" && r.ApproverEmployeeNumber == approverEmployeeNumber);

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
        // Requestor visibility: only they can see their own requests, unless caller is Manager+
        var caller = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == visibleToEmployeeNumber);

        if (caller is null)
        {
            return new PagedResult<RequestDto>([], page, pageSize, 0);
        }

        if (caller.Id != requestorEmployeeNumber && caller.RankLevel < 2)
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
        // Load the user to determine visibility scope
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == employeeNumber);

        if (user is null)
        {
            return new Dictionary<string, int>();
        }

        // Build the query based on visibility (Manager+ see all, others see their own + those they approve)
        IQueryable<Core.Entities.Request> query = db.Requests.AsNoTracking();

        if (user.RankLevel < 2)
        {
            query = query.Where(r =>
                r.RequestorEmployeeNumber == employeeNumber
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

