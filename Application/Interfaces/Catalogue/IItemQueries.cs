using Application.DTOs.Catalogue;
using Application.DTOs.Common;

namespace Application.Interfaces.Catalogue;

/// <summary>Paged/filtered/role-aware reads — implemented in Infrastructure over EF Core LINQ
/// (CLAUDE.md architecture principle #4: IRepository&lt;T&gt; is CRUD-only, joins/paging go here).</summary>
public interface IItemQueries
{
    Task<PagedResult<ItemDto>> GetPagedAsync(ItemQueryParameters parameters, int callerRankLevel);

    Task<ItemDto?> GetByIdAsync(int itemId, int callerRankLevel);

    /// <summary>Unfiltered by rank — the write path (create/update) reloading its own DTO after
    /// a mutation should never be blocked by the caller's rank; it's not the browse endpoint.</summary>
    Task<ItemDto?> GetByIdUnfilteredAsync(int itemId);

    Task<bool> CategoryExistsAsync(int categoryId);
}
