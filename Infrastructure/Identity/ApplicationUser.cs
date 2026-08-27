using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// Identity's Id doubles as the HR-assigned employee number (1-1000, ValueGeneratedNever) —
/// see docs/development/identity-and-user-management-implementation-plan.md §2.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public required string Name { get; set; }

    public string? Grade { get; set; }

    public string? Location { get; set; }

    public int? SuperiorEmployeeNumber { get; set; }

    public ApplicationUser? Superior { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
