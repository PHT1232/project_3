namespace Application.DTOs.Users;

public sealed record UpdateUserRequest(
    string Name,
    string Email,
    string Role,
    int SuperiorEmployeeNumber,
    string? Grade,
    string? Location);
