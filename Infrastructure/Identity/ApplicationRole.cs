using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// RankLevel drives the hierarchy checks in Plan §3/§4.2 (Manager+ = RankLevel >= 2).
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    public int RankLevel { get; set; }
}
