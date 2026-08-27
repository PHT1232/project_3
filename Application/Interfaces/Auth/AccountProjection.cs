namespace Application.Interfaces.Auth;

/// <summary>
/// Application-owned view of an Identity-backed account. Never expose PasswordHash,
/// security stamps, concurrency stamps, or Identity entities past this boundary.
/// </summary>
public sealed record AccountProjection(
    int EmployeeNumber,
    string Name,
    string Email,
    string Role,
    int RankLevel,
    int? SuperiorEmployeeNumber,
    bool IsApprover,
    bool IsActive);
