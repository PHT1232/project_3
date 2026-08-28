using Application.DTOs.Catalogue;
using Application.DTOs.Common;

namespace Application.Interfaces.Catalogue;

/// <summary>Paged/filtered/role-aware reads — implemented in Infrastructure over EF Core LINQ
/// (CLAUDE.md architecture principle #4: IRepository&lt;T&gt; is CRUD-only, joins/paging go here).</summary>
public interface IItemQueries
{
    Task<PagedResult<ItemDto>> GetPagedAsync(ItemQueryParameters parameters, int callerRankLevel);

    Task<ItemDto?> GetByIdAsync(int itemId, int callerRankLevel);

    /// <summary>Unfiltered by rank — used by write paths (create/update/deactivate) that need
    /// the raw entity, not the role-filtered read model.</summary>
    Task<bool> CategoryExistsAsync(int categoryId);
}
