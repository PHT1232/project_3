using Application.DTOs.SupplierRequests;

namespace Application.Interfaces.SupplierRequests;

public interface ISupplierRequestService
{
    /// <summary>
    /// Validates the whole cart, groups the lines by their authoritative supplier, and creates one
    /// SupplierRequest per supplier — all in one transaction, so a single bad line leaves nothing
    /// behind. Does not touch stock.
    /// </summary>
    Task<IReadOnlyList<SupplierRequestDto>> CreateAsync(
        CreateSupplierRequestCommand command, int actorEmployeeNumber);
}
