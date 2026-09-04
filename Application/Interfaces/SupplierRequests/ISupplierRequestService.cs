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

    /// <summary>
    /// Business Manager confirms the goods physically arrived: PendingArrival → Received, and
    /// only now does stock go up — one Receipt ledger row per line, balance and status committed
    /// in a single transaction.
    ///
    /// Idempotent by status: an order that is already Received throws <c>ConflictException</c>
    /// rather than posting a second receipt, so a double-click cannot inflate the balance.
    /// Caller authorisation (Business Manager only) is enforced by the controller policy.
    /// </summary>
    Task<SupplierRequestDto> ConfirmArrivalAsync(int supplierRequestId, int actorEmployeeNumber);
}
