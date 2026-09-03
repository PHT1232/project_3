namespace Application.Interfaces.Support;

using Application.DTOs.Common;
using Application.DTOs.Support;

/// <summary>Read side of the support inbox (Manager+ triage screen).</summary>
public interface ISupportMessageQueries
{
    /// <summary>
    /// Messages newest-first. <paramref name="status"/> filters to "New" / "Resolved";
    /// null or unrecognised returns all.
    /// </summary>
    Task<PagedResult<SupportMessageDto>> GetPagedAsync(string? status, int page, int pageSize);

    /// <summary>One message by id, or null if it does not exist.</summary>
    Task<SupportMessageDto?> GetByIdAsync(int id);

    /// <summary>Count of messages still in "New" — for the inbox badge.</summary>
    Task<int> GetOpenCountAsync();
}
