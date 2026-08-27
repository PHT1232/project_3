namespace Application.DTOs.Auth;

public sealed record CurrentUserDto(
    int EmployeeNumber,
    string Name,
    string Email,
    string Role,
    int RankLevel,
    int? SuperiorEmployeeNumber,
    bool IsApprover);
