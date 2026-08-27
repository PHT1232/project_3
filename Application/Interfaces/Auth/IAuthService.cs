using Application.DTOs.Auth;

namespace Application.Interfaces.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);

    Task<CurrentUserDto?> GetCurrentUserAsync(int employeeNumber);

    Task ChangePasswordAsync(int employeeNumber, ChangePasswordRequest request);
}
