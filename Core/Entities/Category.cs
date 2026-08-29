namespace Core.Entities;

public class Category
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Not in the m2 plan's §2.1 entity table, added here because §3.2's
    /// ICategoryService.DeactivateCategoryAsync needs a field to flip — the plan's own
    /// service interface implies this column without listing it.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
