using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;

namespace VehicleRental.UnitTests.TestSupport;

/// <summary>
/// Fábricas com valores válidos por padrão: cada teste sobrescreve só o que está exercitando,
/// de forma que o assunto do teste fique óbvio na chamada.
/// </summary>
internal static class TestData
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(FixedDateTimeProvider.DefaultInstant);

    public static Vehicle AVehicle(
        string plate = "ABC1D23",
        string brand = "Volkswagen",
        string model = "Gol",
        int year = 2024,
        VehicleCategory category = VehicleCategory.Hatch,
        decimal dailyRate = 100m)
        => Vehicle.Create(
            plate,
            brand,
            model,
            year,
            category,
            dailyRate,
            FixedDateTimeProvider.DefaultInstant.Year,
            FixedDateTimeProvider.DefaultInstant);

    public static User AUser(
        string email = "cliente@teste.com",
        string passwordHash = "$2a$12$hash",
        UserRole role = UserRole.Customer)
        => User.Create(email, passwordHash, role, FixedDateTimeProvider.DefaultInstant);

    public static Customer ACustomer(
        Guid? userId = null,
        string fullName = "Ryan Sarcinelli",
        string driverLicense = "12345678901",
        string phone = "27999990000")
        => Customer.Create(userId ?? Guid.NewGuid(), fullName, driverLicense, phone);

    /// <summary>
    /// Aluguel ativo de <paramref name="days"/> dias começando hoje.
    /// </summary>
    public static Rental ARental(Vehicle vehicle, Guid customerId, int days = 3)
        => Rental.Create(
            vehicle,
            customerId,
            Today,
            Today.AddDays(days),
            Today,
            FixedDateTimeProvider.DefaultInstant);
}
