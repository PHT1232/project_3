using Application.DTOs.Inventory;
using Application.Exceptions;
using Application.Interfaces.Inventory;
using Core.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// The single write path onto the stock ledger (CLAUDE.md architecture principle #5 — balance
/// change + ledger row commit together). Concurrency is a manual RowVersion compare-then-set,
/// not EF's DbUpdateConcurrencyException path — that keeps behavior identical whether RowVersion
/// is configured as a SQL Server rowversion or, as here, an app-managed Guid (m2 plan §4.5).
/// </summary>
public class StockService(DataContext db, IInventoryQueries inventoryQueries) : IStockService
{
    public Task<InventoryRowDto> IssueAsync(int itemId, int quantity, int actorEmployeeNumber, string reference, Guid expectedRowVersion) =>
        ApplyAsync(itemId, -Math.Abs(quantity), StockTransactionType.Issue, reference, null, actorEmployeeNumber, expectedRowVersion);

    public Task<InventoryRowDto> ReceiveAsync(int itemId, int quantity, int? supplierId, string? reference, int actorEmployeeNumber, Guid expectedRowVersion) =>
        ApplyAsync(itemId, Math.Abs(quantity), StockTransactionType.Receipt, reference, supplierId, actorEmployeeNumber, expectedRowVersion);

    public Task<InventoryRowDto> AdjustAsync(int itemId, int changeQuantity, string reason, int actorEmployeeNumber, Guid expectedRowVersion) =>
        ApplyAsync(itemId, changeQuantity, StockTransactionType.Adjustment, reason, null, actorEmployeeNumber, expectedRowVersion);

    private async Task<InventoryRowDto> ApplyAsync(
        int itemId,
        int changeQuantity,
        StockTransactionType txType,
        string? reference,
        int? supplierId,
        int actorEmployeeNumber,
        Guid expectedRowVersion)
    {
        var item = await db.StationeryItems.FirstOrDefaultAsync(i => i.Id == itemId)
            ?? throw new NotFoundException($"Item {itemId} not found.");

        if (item.RowVersion != expectedRowVersion)
        {
            throw new ConflictException("This item's stock was changed by someone else. Reload and try again.");
        }

        if (item.QuantityAvailable + changeQuantity < 0)
        {
            throw new ValidationException(
                [new ValidationFailure("changeQuantity", "This would take the balance below zero.")]);
        }

        item.QuantityAvailable += changeQuantity;
        item.RowVersion = Guid.NewGuid();

        db.StockTransactions.Add(new StockTransaction
        {
            ItemId = itemId,
            TxType = txType,
            ChangeQuantity = changeQuantity,
            UnitCostSnapshot = item.UnitCost,
            Reference = reference,
            SupplierId = supplierId,
            CreatedByEmployeeNumber = actorEmployeeNumber,
        });

        await db.SaveChangesAsync();

        return await inventoryQueries.GetRowAsync(itemId)
            ?? throw new InvalidOperationException("Updated item could not be reloaded.");
    }
}
