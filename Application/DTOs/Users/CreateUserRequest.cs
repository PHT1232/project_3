namespace Application.DTOs.Users;

/// <summary>SuperiorEmployeeNumber == 0 maps to no superior (Plan §3.1's 0 ↔ NULL mapping).</summary>
public sealed record CreateUserRequest(
    int EmployeeNumber,
    string Name,
    string Email,
    string Role,
    int SuperiorEmployeeNumber,
    string InitialPassword,
    string? Grade,
    string? Location);
