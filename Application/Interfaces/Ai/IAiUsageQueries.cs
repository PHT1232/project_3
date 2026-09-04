using Application.DTOs.Ai;
using Application.DTOs.Common;

namespace Application.Interfaces.Ai;

/// <summary>
/// Read side of <c>AiInteractionLogs</c> for <c>GET /api/v1/ai/usage-report</c> (Plan §4.2,
/// T5.6 [RUBRIC]). Paged/ordered read, so it lives here rather than on IRepository
/// (CLAUDE.md principle #4).
/// </summary>
public interface IAiUsageQueries
{
    Task<PagedResult<AiInteractionLogDto>> GetPagedAsync(int page, int pageSize);
}
