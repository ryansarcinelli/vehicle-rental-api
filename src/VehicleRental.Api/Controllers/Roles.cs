namespace VehicleRental.Api.Controllers;

/// <summary>
/// Nomes de perfil como constantes, porque [Authorize(Roles = ...)] exige string literal
/// e um erro de digitação ali abriria o endpoint silenciosamente.
/// </summary>
public static class Roles
{
    public const string Admin = nameof(Domain.Enums.UserRole.Admin);

    public const string Customer = nameof(Domain.Enums.UserRole.Customer);
}
