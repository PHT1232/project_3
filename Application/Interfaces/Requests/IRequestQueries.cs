namespace Application.Interfaces.Requests;

using Application.DTOs.Common;
using Application.DTOs.Requests;

/// <summary>
/// Query service for browsing and filtering stationery requests.
/// All queries enforce ownership/eligibility checks:
/// - Requestors see only their own requests (unless they are an approver).
/// - Approvers see only requests they must approve (their subordinates' requests).
/// - Managers see all requests (for reports, M5).
/// </summary>
public interface IRequestQueries
{
    /// <summary>
    /// Paginated list of requests the caller can see (requestor's own, or those pending
    /// their approval, or all if Manager+).
    /// </summary>
    Task<PagedResult<RequestDto>> GetVisibleAsync(
        int page,
        int pageSize,
        string? statusFilter,
        int visibleToEmployeeNumber
    );

    /// <summary>
    /// Single request by ID, with full history and line items. Returns null if not found.
    /// Caller must own it or be its approver or be a Manager+, or 404 is thrown.
    /// </summary>
    Task<RequestDto?> GetByIdAsync(int requestId, int visibleToEmployeeNumber);

    /// <summary>
    /// Requests awaiting the caller's decision as approver: Pending (approve / reject) and
    /// CancellationPending (approve / refuse the cancellation). Drafts are never included.
    /// </summary>
    Task<PagedResult<RequestDto>> GetPendingApprovalsAsync(
        int page,
        int pageSize,
        int approverEmployeeNumber
    );

    /// <summary>
    /// Requests by requestor (for dashboards showing "my requests").
    /// Caller must be the requestor or a Manager+, or 404 is thrown.
    /// </summary>
    Task<PagedResult<RequestDto>> GetByRequestorAsync(
        int requestorEmployeeNumber,
        int page,
        int pageSize,
        int visibleToEmployeeNumber,
        string? statusFilter = null
    );

    /// <summary>
    /// Count of requests in each status, for the dashboard widget (Plan §3.2, "Request Summary").
    /// Returns only statuses with count > 0.
    /// </summary>
    Task<Dictionary<string, int>> GetStatusSummaryForDashboardAsync(int employeeNumber);
}
