namespace Application.DTOs.Auth;

public sealed record LoginRequest(int EmployeeNumber, string Password);
