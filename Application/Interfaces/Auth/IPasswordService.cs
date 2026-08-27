namespace Application.Interfaces.Auth;

public interface IPasswordService
{
    /// <summary>Empty list = success. Non-empty = human-readable failure reasons.</summary>
    Task<IReadOnlyList<string>> ChangePasswordAsync(int employeeNumber, string currentPassword, string newPassword);
}
