namespace VehicleRental.Application.Abstractions;

/// <summary>
/// Abstrai "agora" para que as regras que dependem da data (reserva no passado,
/// atraso na devolução) sejam determinísticas no teste unitário.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateOnly Today { get; }

    int CurrentYear { get; }
}
