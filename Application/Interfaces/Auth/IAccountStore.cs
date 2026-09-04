namespace Application.Interfaces.Auth;

public interface IAccountStore
{
    Task<AccountVerificationResult> VerifyCredentialsAsync(int employeeNumber, string password);

    /// <summary>
    /// Same contract as <see cref="VerifyCredentialsAsync(int, string)"/> but keyed on the
    /// account's email address, which Identity holds unique and case-insensitive
    /// (<c>RequireUniqueEmail</c> in Program.cs). Unknown, inactive, locked out and wrong
    /// password all return <see cref="AccountVerificationResult.Failed"/> — the caller cannot
    /// tell an unregistered email from a wrong password.
    /// </summary>
    Task<AccountVerificationResult> VerifyCredentialsByEmailAsync(string email, string password);

    Task<AccountProjection?> GetByEmployeeNumberAsync(int employeeNumber);
}
