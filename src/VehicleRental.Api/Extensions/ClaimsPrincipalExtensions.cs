using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VehicleRental.Application.Common;
using VehicleRental.Domain.Enums;

namespace VehicleRental.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Converte os claims do JWT no <see cref="CurrentUser"/> que os services esperam.
    /// É a única ponte entre HTTP e a camada de aplicação em matéria de identidade.
    /// </summary>
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAccessException("Token sem identificador de usuário válido.");

        var role = principal.FindFirstValue(ClaimTypes.Role);

        if (!Enum.TryParse<UserRole>(role, out var parsedRole))
            throw new UnauthorizedAccessException("Token sem perfil válido.");

        return new CurrentUser(userId, parsedRole);
    }
}
