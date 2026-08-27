namespace Application.Interfaces.Auth;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) CreateToken(AccountProjection account);
}
