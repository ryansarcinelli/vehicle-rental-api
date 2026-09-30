using VehicleRental.Domain.Common;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.Domain.Entities;

public class Vehicle : Entity
{
    public const int OldestAllowedYear = 1900;

    public Plate Plate { get; private set; } = null!;
    public string Brand { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public int Year { get; private set; }
    public VehicleCategory Category { get; private set; }
    public Money DailyRate { get; private set; } = null!;
    public VehicleStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core
    private Vehicle()
    {
    }

    public static Vehicle Create(
        string plate,
        string brand,
        string model,
        int year,
        VehicleCategory category,
        decimal dailyRate,
        int currentYear,
        DateTime createdAt)
    {
        return new Vehicle
        {
            Plate = Plate.Create(plate),
            Brand = RequireText(brand, nameof(brand)),
            Model = RequireText(model, nameof(model)),
            Year = ValidYear(year, currentYear),
            Category = ValidCategory(category),
            DailyRate = Money.Create(dailyRate),
            Status = VehicleStatus.Available,
            CreatedAt = createdAt
        };
    }

    public void UpdateDetails(
        string plate,
        string brand,
        string model,
        int year,
        VehicleCategory category,
        decimal dailyRate,
        int currentYear)
    {
        Plate = Plate.Create(plate);
        Brand = RequireText(brand, nameof(brand));
        Model = RequireText(model, nameof(model));
        Year = ValidYear(year, currentYear);
        Category = ValidCategory(category);
        DailyRate = Money.Create(dailyRate);
    }

    public void MarkAsRented()
    {
        if (Status != VehicleStatus.Available)
            throw new DomainException($"Veículo {Plate} não está disponível (status atual: {Status}).");

        Status = VehicleStatus.Rented;
    }

    public void MarkAsAvailable()
    {
        if (Status == VehicleStatus.Available)
            throw new DomainException($"Veículo {Plate} já está disponível.");

        Status = VehicleStatus.Available;
    }

    public void SendToMaintenance()
    {
        if (Status == VehicleStatus.Rented)
            throw new DomainException($"Veículo {Plate} está alugado e não pode entrar em manutenção.");

        Status = VehicleStatus.Maintenance;
    }

    /// <summary>
    /// Regra 8: veículo com aluguel ativo não pode ser removido da frota.
    /// </summary>
    public void EnsureCanBeDeleted()
    {
        if (Status == VehicleStatus.Rented)
            throw new DomainException($"Veículo {Plate} está alugado e não pode ser excluído.");
    }

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"O campo '{field}' é obrigatório.");

        return value.Trim();
    }

    private static int ValidYear(int year, int currentYear)
    {
        var newest = currentYear + 1;

        if (year < OldestAllowedYear || year > newest)
            throw new DomainException($"Ano inválido: {year}. Informe um valor entre {OldestAllowedYear} e {newest}.");

        return year;
    }

    private static VehicleCategory ValidCategory(VehicleCategory category)
    {
        if (!Enum.IsDefined(category))
            throw new DomainException($"Categoria inválida: {(int)category}.");

        return category;
    }
}
