namespace Application.DTOs.Users;

public sealed record UserDto(
    int EmployeeNumber,
    string Name,
    string Email,
    string Role,
    int RankLevel,
    int? SuperiorEmployeeNumber,
    string? Grade,
    string? Location,
    bool IsActive);
