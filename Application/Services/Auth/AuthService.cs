using Application.DTOs.Auth;
using Application.Interfaces.Auth;

namespace Application.Services.Auth;

public class AuthService(IAccountStore accountStore, ITokenService tokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var result = await accountStore.VerifyCredentialsAsync(request.EmployeeNumber, request.Password);
        if (!result.Succeeded || result.Account is null)
        {
            return null;
        }

        var (accessToken, expiresAtUtc) = tokenService.CreateToken(result.Account);
        return new LoginResponse(accessToken, expiresAtUtc, ToCurrentUserDto(result.Account));
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(int employeeNumber)
    {
        var account = await accountStore.GetByEmployeeNumberAsync(employeeNumber);
        return account is null ? null : ToCurrentUserDto(account);
    }

    private static CurrentUserDto ToCurrentUserDto(AccountProjection account) => new(
        account.EmployeeNumber,
        account.Name,
        account.Email,
        account.Role,
        account.RankLevel,
        account.SuperiorEmployeeNumber,
        account.IsApprover);
}
