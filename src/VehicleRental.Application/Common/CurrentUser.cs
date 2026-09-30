using VehicleRental.Domain.Enums;

namespace VehicleRental.Application.Common;

/// <summary>
/// Identidade do chamador, passada explicitamente para os services em vez de lida de
/// um estado ambiente (HttpContext). Isso mantém a Application independente de HTTP e
/// torna trivial testar autorização.
/// </summary>
public sealed record CurrentUser(Guid UserId, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
}
