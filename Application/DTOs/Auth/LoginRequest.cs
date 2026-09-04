namespace Application.DTOs.Auth;

/// <summary>
/// Sign-in credentials. Exactly one identifier must be supplied — <see cref="EmployeeNumber"/>
/// (the Plan's login field, §3.1) or <see cref="Email"/>. Both are optional at the type level so
/// existing callers that only ever send an employee number keep working unchanged;
/// <c>LoginRequestValidator</c> enforces the "exactly one" rule.
/// </summary>
public sealed record LoginRequest(int? EmployeeNumber, string? Email, string Password);
