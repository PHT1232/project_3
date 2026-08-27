using Application.DTOs.Users;

namespace Application.Interfaces.Users;

/// <summary>Raw persistence operations, implemented in Infrastructure over UserManager/RoleManager.</summary>
public interface IUserStore
{
    Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize, string? role, string? location);

    Task<UserDto?> GetByEmployeeNumberAsync(int employeeNumber);

    Task<bool> RoleExistsAsync(string role);

    Task<bool> EmployeeExistsAsync(int employeeNumber);

    Task<bool> EmailExistsAsync(string email, int? excludeEmployeeNumber);

    Task<int?> GetSuperiorEmployeeNumberAsync(int employeeNumber);

    Task<UserDto> CreateUserAsync(CreateUserRequest request);

    Task<UserDto?> UpdateUserAsync(int employeeNumber, UpdateUserRequest request);

    Task<UserDto?> SetStatusAsync(int employeeNumber, bool isActive);

    Task<IReadOnlyList<UserDto>> GetSubordinatesAsync(int employeeNumber);
}
