using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Application.Interfaces.Notifications;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.Auth;

public class AuthService(
    IAccountStore accountStore,
    ITokenService tokenService,
    IPasswordService passwordService,
    INotificationService notificationService,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator) : IAuthService
{
    /// <summary>
    /// Signs in by employee number (the Plan's login field, §3.1) or by email address, whichever
    /// the caller supplied. Which identifier was used changes nothing after the lookup: the same
    /// verification runs, and every failure returns null so the controller can answer with one
    /// generic 401 (Plan §9.2 — never leak which check failed, or whether an account exists).
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        await loginValidator.ValidateAndThrowAsync(request);

        var result = request.EmployeeNumber is { } employeeNumber
            ? await accountStore.VerifyCredentialsAsync(employeeNumber, request.Password)
            : await accountStore.VerifyCredentialsByEmailAsync(request.Email!, request.Password);

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
    /// Performs the password change and security-stamp rotation, then fires the 6th
    /// notification trigger (Plan §4.2 [SPEC]) to the user and their superior. The
    /// notification write happens after — and separately from — the password change itself;
    /// see INotificationService.NotifyPasswordChangedAsync's doc comment for why.
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

        await notificationService.NotifyPasswordChangedAsync(employeeNumber);
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
