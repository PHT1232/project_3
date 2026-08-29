namespace Core.Entities;

public class Supplier
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int LeadTimeDays { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>App-managed concurrency token (see m2 plan §4.1) — reassigned to a new Guid on
    /// every mutating save, not a SQL Server native rowversion column.</summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
