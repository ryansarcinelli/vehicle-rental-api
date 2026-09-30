using VehicleRental.Application.Abstractions;

namespace VehicleRental.UnitTests.TestSupport;

/// <summary>
/// Relógio parado, para que testes de "reserva no passado" e "devolução atrasada"
/// não dependam do dia em que rodam.
/// </summary>
internal sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public static readonly DateTime DefaultInstant = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    public FixedDateTimeProvider() : this(DefaultInstant)
    {
    }

    public DateTime UtcNow { get; } = utcNow;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);

    public int CurrentYear => UtcNow.Year;
}
