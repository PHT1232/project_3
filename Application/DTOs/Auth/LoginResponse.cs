namespace Application.DTOs.Auth;

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentUserDto User);
