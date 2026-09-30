namespace VehicleRental.Domain.Exceptions;

/// <summary>
/// Regra de negócio violada. A API traduz para 409 Conflict.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
