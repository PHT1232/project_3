using Application.Interfaces.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Reporting-line lookups (see <see cref="IHierarchyQueries"/>). Rank is read from the
/// assigned Identity role (<c>AspNetUserRoles → AspNetRoles.RankLevel</c>), not the vestigial
/// <c>ApplicationUser.RankLevel</c> — the same resolution <see cref="ReportQueries"/> and
/// <see cref="EligibilityQueries"/> use (CLAUDE.md K8).
///
/// The sub-tree walk pulls the (small) <c>Id → SuperiorEmployeeNumber</c> adjacency for the
/// whole user table once and expands it in memory: an eProject has tens of users, not a
/// recursive-CTE-sized org chart, and this stays provider-portable across SQL Server and the
/// SQLite test provider.
/// </summary>
public class HierarchyQueries(DataContext db) : IHierarchyQueries
{
    private const int ManagingDirectorRank = 4;

    public async Task<IReadOnlySet<int>?> GetVisibleRequestorScopeAsync(int actor, int maxDepth = 10)
    {
        var rank = await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == actor
            select (int?)r.RankLevel
        ).FirstOrDefaultAsync() ?? 0;

        if (rank >= ManagingDirectorRank)
        {
            return null;
        }

        var childrenByParent = (await db.Users
                .AsNoTracking()
                .Where(u => u.SuperiorEmployeeNumber != null)
                .Select(u => new { u.Id, ParentId = u.SuperiorEmployeeNumber!.Value })
                .ToListAsync())
            .GroupBy(x => x.ParentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var visible = new HashSet<int> { actor };
        var frontier = new List<int> { actor };

        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var next = new List<int>();
            foreach (var parent in frontier)
            {
                if (!childrenByParent.TryGetValue(parent, out var children))
                {
                    continue;
                }

                foreach (var child in children)
                {
                    if (visible.Add(child))
                    {
                        next.Add(child);
                    }
                }
            }

            frontier = next;
        }

        return visible;
    }
}
