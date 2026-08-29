using Application.DTOs.Common;
using Application.DTOs.Users;

namespace Application.Interfaces.Users;

public interface IUserManagementService
{
    Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize, string? role, string? location);

    Task<UserDto> CreateUserAsync(CreateUserRequest request);

    Task<UserDto> UpdateUserAsync(int employeeNumber, UpdateUserRequest request);

    Task<UserDto> SetStatusAsync(int employeeNumber, bool isActive);

    Task<IReadOnlyList<UserDto>> GetSubordinatesAsync(int employeeNumber);
}
