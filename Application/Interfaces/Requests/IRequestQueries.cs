namespace Application.Interfaces.Requests;

using Application.DTOs.Common;
using Application.DTOs.Requests;

/// <summary>
/// Query service for browsing and filtering stationery requests.
/// Row-level visibility follows the reporting line (Plan §6, TC-15):
/// - Everyone sees their own requests, and any request pending their own approval.
/// - Manager / Business Manager also see every request raised by someone in their reporting
///   sub-tree (reports, and their reports, to any depth) — never a peer's or a superior's.
/// - Managing Director sees all requests.
/// </summary>
public interface IRequestQueries
{
    /// <summary>
    /// Paginated list of requests visible to the caller (own, pending their approval, or
    /// raised anywhere in their reporting sub-tree; unrestricted for the Managing Director).
    /// </summary>
    Task<PagedResult<RequestDto>> GetVisibleAsync(
        int page,
        int pageSize,
        string? statusFilter,
        int visibleToEmployeeNumber
    );

    /// <summary>
    /// Single request by ID, with full history and line items. Returns null when it does not
    /// exist or is not visible to the caller (own / approver / inside their sub-tree) — the
    /// caller cannot tell the two apart, so existence is not leaked.
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
    /// Caller must be the requestor, or have that requestor inside their reporting sub-tree,
    /// otherwise an empty page is returned.
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
