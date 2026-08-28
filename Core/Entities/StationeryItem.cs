namespace Core.Entities;

public class StationeryItem
{
    public int Id { get; set; }

    public required string ItemName { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public required string UnitOfMeasure { get; set; }

    public decimal UnitCost { get; set; }

    /// <summary>Cached balance — StockTransactions is the source of truth (Plan §2 ledger rule).</summary>
    public int QuantityAvailable { get; set; }

    public int ReorderLevel { get; set; }

    /// <summary>Engineer=1, Manager=2, Business Manager=3, MD=4. Role-filters the catalogue.</summary>
    public int MinRankLevelToRequest { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    /// <summary>App-managed concurrency token — see m2 plan §4.1.</summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
