namespace VehicleRental.Application.Auth.Dtos;

/// <summary>
/// Cadastro de cliente. Perfil Admin não é criado por esta rota — vem do seed.
/// </summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string DriverLicense,
    string Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string Email,
    string Role);
