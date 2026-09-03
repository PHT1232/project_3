namespace Application.Interfaces.Users;

/// <summary>
/// Reporting-line lookups over the self-referencing <c>Users.SuperiorEmployeeNumber</c> chain
/// (Plan §1.1, §3.1). Shared by any feature that scopes rows to "me and the people below me".
/// </summary>
public interface IHierarchyQueries
{
    /// <summary>
    /// The set of requestor employee numbers whose requests <paramref name="actor"/> may see:
    /// the actor plus everyone in the reporting sub-tree beneath them, walked to any depth.
    /// Returns <c>null</c> when the actor's rank is unrestricted (Managing Director) — callers
    /// treat <c>null</c> as "no row filter".
    ///
    /// The walk is bounded to <paramref name="maxDepth"/> levels and de-duplicates visited
    /// nodes, so a malformed hierarchy (a cycle introduced despite the create/update guard in
    /// Plan §M1 T1.5) terminates instead of looping.
    /// </summary>
    Task<IReadOnlySet<int>?> GetVisibleRequestorScopeAsync(int actor, int maxDepth = 10);
}
