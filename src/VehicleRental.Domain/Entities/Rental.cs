using VehicleRental.Domain.Common;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.Domain.Entities;

/// <summary>
/// Aluguel de um veículo por um cliente em um período.
/// Concentra as regras de cálculo de valor, multa por atraso e transição de status —
/// é por isso que elas são testáveis sem banco.
/// </summary>
public class Rental : Entity
{
    public const int MaxDurationInDays = 90;
    public const int LongRentalThresholdInDays = 7;
    public const int ExtendedRentalThresholdInDays = 30;
    public const decimal LongRentalDiscountPercentage = 10m;
    public const decimal ExtendedRentalDiscountPercentage = 15m;
    public const decimal LateFeeMultiplier = 1.5m;

    public Guid VehicleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateOnly? ReturnedAt { get; private set; }

    /// <summary>
    /// Diária congelada no momento da reserva: alterar o preço do veículo depois
    /// não muda o valor de aluguéis já criados.
    /// </summary>
    public Money DailyRateSnapshot { get; private set; } = null!;

    public Money TotalAmount { get; private set; } = null!;
    public Money LateFee { get; private set; } = Money.Zero;
    public RentalStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Vehicle? Vehicle { get; private set; }
    public Customer? Customer { get; private set; }

    // EF Core
    private Rental()
    {
    }

    public static Rental Create(
        Vehicle vehicle,
        Guid customerId,
        DateOnly startDate,
        DateOnly endDate,
        DateOnly today,
        DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (customerId == Guid.Empty)
            throw new DomainException("O cliente é obrigatório.");

        if (endDate <= startDate)
            throw new DomainException("A data de devolução deve ser posterior à data de retirada.");

        if (startDate < today)
            throw new DomainException("A data de retirada não pode estar no passado.");

        var days = endDate.DayNumber - startDate.DayNumber;

        if (days > MaxDurationInDays)
            throw new DomainException($"O período máximo de aluguel é de {MaxDurationInDays} dias (solicitado: {days}).");

        // Lança se o veículo não estiver disponível (regra 7).
        vehicle.MarkAsRented();

        return new Rental
        {
            VehicleId = vehicle.Id,
            CustomerId = customerId,
            StartDate = startDate,
            EndDate = endDate,
            DailyRateSnapshot = vehicle.DailyRate,
            TotalAmount = CalculateTotal(vehicle.DailyRate, days),
            LateFee = Money.Zero,
            Status = RentalStatus.Active,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Regra 5: dias × diária, com desconto de 10% a partir de 7 dias e 15% a partir de 30.
    /// </summary>
    public static Money CalculateTotal(Money dailyRate, int days)
    {
        if (days <= 0)
            throw new DomainException("O período de aluguel deve ser de pelo menos um dia.");

        var gross = dailyRate.Multiply(days);

        var discount = days switch
        {
            >= ExtendedRentalThresholdInDays => ExtendedRentalDiscountPercentage,
            >= LongRentalThresholdInDays => LongRentalDiscountPercentage,
            _ => 0m
        };

        return gross.ApplyDiscount(discount);
    }

    /// <summary>
    /// Regra 6: devolução após a data prevista gera multa = dias de atraso × diária × 1.5.
    /// </summary>
    public void Return(Vehicle vehicle, DateOnly returnDate)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        EnsureSameVehicle(vehicle);

        if (Status != RentalStatus.Active)
            throw new DomainException($"Somente aluguéis ativos podem ser devolvidos (status atual: {Status}).");

        if (returnDate < StartDate)
            throw new DomainException("A data de devolução não pode ser anterior à data de retirada.");

        var lateDays = returnDate.DayNumber - EndDate.DayNumber;

        LateFee = lateDays > 0
            ? DailyRateSnapshot.Multiply(lateDays * LateFeeMultiplier)
            : Money.Zero;

        ReturnedAt = returnDate;
        Status = RentalStatus.Completed;
        vehicle.MarkAsAvailable();
    }

    /// <summary>
    /// Cancelamento só é permitido antes da retirada; depois disso a saída é a devolução.
    /// </summary>
    public void Cancel(Vehicle vehicle, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        EnsureSameVehicle(vehicle);

        if (Status != RentalStatus.Active)
            throw new DomainException($"Somente aluguéis ativos podem ser cancelados (status atual: {Status}).");

        if (today >= StartDate)
            throw new DomainException("O aluguel já foi iniciado e não pode ser cancelado; registre a devolução.");

        Status = RentalStatus.Cancelled;
        vehicle.MarkAsAvailable();
    }

    public Money AmountDue => TotalAmount.Add(LateFee);

    public int DurationInDays => EndDate.DayNumber - StartDate.DayNumber;

    public bool BelongsTo(Guid customerId) => CustomerId == customerId;

    private void EnsureSameVehicle(Vehicle vehicle)
    {
        if (vehicle.Id != VehicleId)
            throw new DomainException("O veículo informado não corresponde ao veículo do aluguel.");
    }
}
