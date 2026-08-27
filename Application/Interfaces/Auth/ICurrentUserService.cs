namespace Application.Interfaces.Auth;

/// <summary>
/// Parses/validates the current request's employee number, role, and rank claims.
/// Application services consult this for ownership/hierarchy checks; it does not itself
/// grant or deny access — policies at the controller are the primary control (Plan §9.4).
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    int? EmployeeNumber { get; }

    string? Role { get; }

    int? RankLevel { get; }
}
