namespace Application.DTOs.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
