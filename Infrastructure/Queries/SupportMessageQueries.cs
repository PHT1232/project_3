using Application.DTOs.Common;
using Application.DTOs.Support;
using Application.Interfaces.Support;
using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>Read side of the support inbox — newest-first list + open count for the badge.</summary>
public class SupportMessageQueries(DataContext db) : ISupportMessageQueries
{
    public async Task<PagedResult<SupportMessageDto>> GetPagedAsync(string? status, int page, int pageSize)
    {
        IQueryable<SupportMessage> query = db.SupportMessages.AsNoTracking();

        if (SupportMessageStatus.All.Contains(status))
        {
            query = query.Where(m => m.Status == status);
        }

        var totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Resolve sender / resolver names in one round-trip rather than per row.
        var ids = rows
            .SelectMany(m => m.ResolvedByEmployeeNumber is { } r ? new[] { m.SenderEmployeeNumber, r } : [m.SenderEmployeeNumber])
            .Distinct()
            .ToArray();

        var names = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var items = rows.Select(m => new SupportMessageDto(
            m.Id,
            m.SenderEmployeeNumber,
            names.GetValueOrDefault(m.SenderEmployeeNumber, ""),
            m.Area,
            m.Subject,
            m.Body,
            m.Diagnostics,
            m.Status,
            m.CreatedAtUtc,
            m.ResolvedAtUtc,
            m.ResolvedByEmployeeNumber is { } r ? names.GetValueOrDefault(r) : null)).ToList();

        return new PagedResult<SupportMessageDto>(items, page, pageSize, totalCount);
    }

    public async Task<SupportMessageDto?> GetByIdAsync(int id)
    {
        var m = await db.SupportMessages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null)
        {
            return null;
        }

        var names = await db.Users.AsNoTracking()
            .Where(u => u.Id == m.SenderEmployeeNumber || u.Id == m.ResolvedByEmployeeNumber)
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        return new SupportMessageDto(
            m.Id,
            m.SenderEmployeeNumber,
            names.GetValueOrDefault(m.SenderEmployeeNumber, ""),
            m.Area,
            m.Subject,
            m.Body,
            m.Diagnostics,
            m.Status,
            m.CreatedAtUtc,
            m.ResolvedAtUtc,
            m.ResolvedByEmployeeNumber is { } r ? names.GetValueOrDefault(r) : null);
    }

    public Task<int> GetOpenCountAsync() =>
        db.SupportMessages.AsNoTracking().CountAsync(m => m.Status == SupportMessageStatus.New);
}
