using Application.DTOs.Ai;
using Application.DTOs.Common;
using Application.Interfaces.Ai;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class AiUsageQueries(DataContext db) : IAiUsageQueries
{
    public async Task<PagedResult<AiInteractionLogDto>> GetPagedAsync(int page, int pageSize)
    {
        var query = db.AiInteractionLogs.AsNoTracking();
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AiInteractionLogDto(
                l.Id, l.EmployeeNumber, l.Feature, l.Model, l.LatencyMs, l.WasFallback, l.FallbackReason,
                l.InputTokens, l.OutputTokens, l.UserText, l.DraftItemCount, l.CreatedAtUtc))
            .ToListAsync();

        return new PagedResult<AiInteractionLogDto>(items, page, pageSize, totalCount);
    }
}
