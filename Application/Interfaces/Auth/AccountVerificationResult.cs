namespace Application.Interfaces.Auth;

/// <summary>
/// Unknown, inactive, locked-out, and bad-password outcomes all collapse to Failed
/// so the API can return one generic 401 (Plan §9.2 — never leak which check failed).
/// </summary>
public sealed record AccountVerificationResult(bool Succeeded, AccountProjection? Account)
{
    public static readonly AccountVerificationResult Failed = new(false, null);

    public static AccountVerificationResult Success(AccountProjection account) => new(true, account);
}
