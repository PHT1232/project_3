using Application.DTOs.Inventory;
using Application.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class StockQueries(DataContext db) : IStockQueries
{
    public async Task<IReadOnlyList<StockTransactionDto>> GetHistoryAsync(int itemId)
    {
        // db.Users is IdentityDbContext's DbSet<ApplicationUser> (AspNetUsers) — there is no
        // separate "Users" table to join against (K8, m2 plan §2.3).
        var rows = await (
                from t in db.StockTransactions
                join u in db.Users on t.CreatedByEmployeeNumber equals u.Id
                where t.ItemId == itemId
                orderby t.CreatedAtUtc descending
                select new
                {
                    t.Id,
                    t.ItemId,
                    t.TxType,
                    t.ChangeQuantity,
                    t.UnitCostSnapshot,
                    t.Reference,
                    t.SupplierId,
                    t.CreatedAtUtc,
                    t.CreatedByEmployeeNumber,
                    CreatedByName = u.Name,
                })
            .ToListAsync();

        return rows
            .Select(r => new StockTransactionDto(
                r.Id, r.ItemId, r.TxType.ToString(), r.ChangeQuantity, r.UnitCostSnapshot,
                r.Reference, r.SupplierId, r.CreatedAtUtc, r.CreatedByEmployeeNumber, r.CreatedByName))
            .ToList();
    }
}
