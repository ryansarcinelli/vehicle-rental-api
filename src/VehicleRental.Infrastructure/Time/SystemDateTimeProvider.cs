using VehicleRental.Application.Abstractions;

namespace VehicleRental.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public int CurrentYear => DateTime.UtcNow.Year;
}
