using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.Auth;

public class AuthService(
    IAccountStore accountStore,
    ITokenService tokenService,
    IPasswordService passwordService,
    IValidator<ChangePasswordRequest> changePasswordValidator) : IAuthService
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

    /// <summary>
    /// TC-14 is NOT fully satisfied by this method alone: the Plan requires notifying both
    /// the user and their superior in the same transaction, and notification infrastructure
    /// does not exist yet (see docs/development/identity-and-user-management-implementation-plan.md
    /// §9). This only performs the password change and security-stamp rotation.
    /// </summary>
    public async Task ChangePasswordAsync(int employeeNumber, ChangePasswordRequest request)
    {
        await changePasswordValidator.ValidateAndThrowAsync(request);

        var errors = await passwordService.ChangePasswordAsync(
            employeeNumber, request.CurrentPassword, request.NewPassword);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors.Select(e => new ValidationFailure(string.Empty, e)));
        }
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
