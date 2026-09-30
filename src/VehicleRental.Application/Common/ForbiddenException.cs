namespace VehicleRental.Application.Common;

/// <summary>
/// Chamador autenticado, mas sem permissão sobre o recurso. A API traduz para 403.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
