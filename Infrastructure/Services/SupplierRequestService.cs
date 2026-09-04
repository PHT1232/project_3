using Application.DTOs.SupplierRequests;
using Application.Exceptions;
using Application.Interfaces.Inventory;
using Application.Interfaces.SupplierRequests;
using Core.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Creates supplier replenishment orders from an inventory cart.
///
/// Lives in Infrastructure, not Application, for the same reason StockService does: it needs
/// DataContext to write several entities in one unit of work, and Application must never
/// reference DbContext (CLAUDE.md architecture principle #1).
///
/// Two rules this class exists to enforce:
///  1. The supplier is the database's to decide, not the client's. If the item has a preferred
///     supplier, that wins outright; the client-supplied SupplierId is only consulted for items
///     that have none, and even then it must resolve to an active supplier.
///  2. Nothing is written until every line has passed. All validation happens before the first
///     Add, and everything commits through a single SaveChangesAsync, so a cart with one bad line
///     leaves no partial order behind.
///
/// Creating an order deliberately does NOT touch stock: it is recorded as PendingArrival, and
/// the balance only moves when a Business Manager confirms the goods physically arrived
/// (<see cref="ConfirmArrivalAsync"/>). Ordering and receiving are separate events.
/// </summary>
public class SupplierRequestService(
    DataContext db,
    ISupplierRequestQueries supplierRequestQueries,
    IStockService stockService,
    IValidator<CreateSupplierRequestCommand> validator) : ISupplierRequestService
{
    public async Task<IReadOnlyList<SupplierRequestDto>> CreateAsync(
        CreateSupplierRequestCommand command, int actorEmployeeNumber)
    {
        await validator.ValidateAndThrowAsync(command);

        var requestedIds = command.Items.Select(l => l.ItemId).ToList();

        var items = await db.StationeryItems
            .Where(i => requestedIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var failures = new List<ValidationFailure>();

        var missing = requestedIds.Where(id => !items.ContainsKey(id)).ToList();
        foreach (var id in missing)
        {
            failures.Add(new ValidationFailure("items", $"Item {id} does not exist."));
        }

        var inactive = items.Values.Where(i => !i.IsActive).Select(i => i.Id).ToList();
        foreach (var id in inactive)
        {
            failures.Add(new ValidationFailure("items", $"Item {id} is inactive and cannot be ordered."));
        }

        // Resolve each line's supplier before writing anything, so an unresolvable line fails the
        // whole submission rather than producing a partial set of orders.
        var resolved = new List<(SupplierRequestLineInput Line, StationeryItem Item, int SupplierId)>();

        foreach (var line in command.Items)
        {
            if (!items.TryGetValue(line.ItemId, out var item) || !item.IsActive)
            {
                continue; // already reported above
            }

            var supplierId = item.SupplierId ?? line.SupplierId;

            if (supplierId is null)
            {
                failures.Add(new ValidationFailure(
                    "items",
                    $"Item {item.Id} ({item.ItemName}) has no preferred supplier — choose one for this line."));
                continue;
            }

            resolved.Add((line, item, supplierId.Value));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var supplierIds = resolved.Select(r => r.SupplierId).Distinct().ToList();

        var suppliers = await db.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        foreach (var supplierId in supplierIds)
        {
            if (!suppliers.TryGetValue(supplierId, out var supplier))
            {
                failures.Add(new ValidationFailure("items", $"Supplier {supplierId} does not exist."));
            }
            else if (!supplier.IsActive)
            {
                failures.Add(new ValidationFailure(
                    "items", $"Supplier {supplier.Name} is inactive and cannot be ordered from."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var created = new List<SupplierRequest>();

        foreach (var group in resolved.GroupBy(r => r.SupplierId))
        {
            var request = new SupplierRequest
            {
                SupplierId = group.Key,
                CreatedByEmployeeNumber = actorEmployeeNumber,
                Status = SupplierRequest.StatusPendingArrival,
            };

            foreach (var (line, item, _) in group)
            {
                request.Items.Add(new SupplierRequestItem
                {
                    ItemId = item.Id,
                    Quantity = line.Quantity,
                    UnitCostSnapshot = item.UnitCost,
                    LineTotal = item.UnitCost * line.Quantity,
                });
            }

            request.TotalCost = request.Items.Sum(i => i.LineTotal);
            created.Add(request);
        }

        db.SupplierRequests.AddRange(created);

        // One save for every order and line — DbContext is the unit of work (Plan §2.4: no
        // UnitOfWork wrapper), so this is all-or-nothing without an explicit transaction.
        await db.SaveChangesAsync();

        var result = new List<SupplierRequestDto>(created.Count);

        foreach (var request in created.OrderBy(r => r.Id))
        {
            result.Add(await supplierRequestQueries.GetByIdAsync(request.Id)
                ?? throw new InvalidOperationException(
                    $"Supplier request {request.Id} could not be reloaded after creation."));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SupplierRequestDto> ConfirmArrivalAsync(int supplierRequestId, int actorEmployeeNumber)
    {
        var request = await db.SupplierRequests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == supplierRequestId)
            ?? throw new NotFoundException($"Supplier order {supplierRequestId} not found.");

        // The duplicate-confirmation guard. An already-Received order is rejected outright rather
        // than posting a second set of receipts, so pressing "Confirm Arrival" twice — or two
        // Business Managers doing it — cannot inflate the balance.
        if (request.Status != SupplierRequest.StatusPendingArrival)
        {
            throw new ConflictException(
                $"Supplier order {supplierRequestId} is already {request.Status}; arrival can only be confirmed once.");
        }

        if (request.Items.Count == 0)
        {
            throw new ConflictException($"Supplier order {supplierRequestId} has no lines to receive.");
        }

        // Stage one Receipt per line. Nothing is written until the single SaveChangesAsync below,
        // so the balances, the ledger rows and the status flip commit together or not at all
        // (CLAUDE.md principle #5 — every balance change writes its ledger row in the same
        // transaction — and #6 — a state change is one atomic transaction).
        var reference = $"Supplier order #{request.Id}";

        foreach (var line in request.Items)
        {
            await stockService.StageReceiptAsync(
                line.ItemId, line.Quantity, request.SupplierId, reference, actorEmployeeNumber);
        }

        request.Status = SupplierRequest.StatusReceived;
        request.ReceivedAtUtc = DateTime.UtcNow;
        request.ReceivedByEmployeeNumber = actorEmployeeNumber;

        await db.SaveChangesAsync();

        return await supplierRequestQueries.GetByIdAsync(request.Id)
            ?? throw new InvalidOperationException(
                $"Supplier request {request.Id} could not be reloaded after confirmation.");
    }
}
