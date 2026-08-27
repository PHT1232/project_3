namespace Application.Interfaces.Auth;

public interface IAccountStore
{
    Task<AccountVerificationResult> VerifyCredentialsAsync(int employeeNumber, string password);

    Task<AccountProjection?> GetByEmployeeNumberAsync(int employeeNumber);
}
