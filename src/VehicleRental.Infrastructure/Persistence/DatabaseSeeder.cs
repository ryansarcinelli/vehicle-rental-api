using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VehicleRental.Application.Abstractions;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;

namespace VehicleRental.Infrastructure.Persistence;

/// <summary>
/// Popula a base com um administrador e uma frota inicial, para que a API seja
/// utilizável no primeiro `docker compose up` sem nenhum passo manual.
/// Idempotente: não duplica nada se rodar de novo.
/// </summary>
public sealed class DatabaseSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    ILogger<DatabaseSeeder> logger)
{
    public const string AdminEmail = "admin@vehiclerental.com";

    public async Task SeedAsync(string adminPassword, CancellationToken cancellationToken = default)
    {
        await SeedAdminAsync(adminPassword, cancellationToken);
        await SeedFleetAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(string adminPassword, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Email == AdminEmail, cancellationToken))
            return;

        context.Users.Add(User.Create(
            AdminEmail,
            passwordHasher.Hash(adminPassword),
            UserRole.Admin,
            clock.UtcNow));

        logger.LogInformation("Seed: administrador {Email} criado.", AdminEmail);
    }

    private async Task SeedFleetAsync(CancellationToken cancellationToken)
    {
        if (await context.Vehicles.AnyAsync(cancellationToken))
            return;

        var fleet = new[]
        {
            ("ABC1D23", "Volkswagen", "Gol", 2023, VehicleCategory.Hatch, 120.00m),
            ("DEF2E34", "Chevrolet", "Onix", 2024, VehicleCategory.Hatch, 135.50m),
            ("GHI3F45", "Toyota", "Corolla", 2024, VehicleCategory.Sedan, 210.00m),
            ("JKL4G56", "Honda", "Civic", 2023, VehicleCategory.Sedan, 225.90m),
            ("MNO5H67", "Jeep", "Compass", 2024, VehicleCategory.SUV, 320.00m),
            ("PQR6I78", "Volkswagen", "T-Cross", 2023, VehicleCategory.SUV, 280.00m),
            ("STU7J89", "Toyota", "Hilux", 2024, VehicleCategory.Pickup, 450.00m),
            ("VWX8K90", "Ford", "Ranger", 2023, VehicleCategory.Pickup, 430.75m)
        };

        foreach (var (plate, brand, model, year, category, dailyRate) in fleet)
        {
            context.Vehicles.Add(Vehicle.Create(
                plate,
                brand,
                model,
                year,
                category,
                dailyRate,
                clock.CurrentYear,
                clock.UtcNow));
        }

        logger.LogInformation("Seed: {Count} veículos adicionados à frota.", fleet.Length);
    }
}
