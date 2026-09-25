using VehicleRental.Domain.Entities;

namespace VehicleRental.Application.Rentals.Dtos;

public sealed record CreateRentalRequest(
    Guid VehicleId,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record ReturnRentalRequest(DateOnly? ReturnDate);

public sealed record RentalResponse(
    Guid Id,
    Guid VehicleId,
    string? VehiclePlate,
    Guid CustomerId,
    string? CustomerName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? ReturnedAt,
    int DurationInDays,
    decimal DailyRate,
    decimal TotalAmount,
    decimal LateFee,
    decimal AmountDue,
    string Status)
{
    public static RentalResponse From(Rental rental) => new(
        rental.Id,
        rental.VehicleId,
        rental.Vehicle?.Plate.Value,
        rental.CustomerId,
        rental.Customer?.FullName,
        rental.StartDate,
        rental.EndDate,
        rental.ReturnedAt,
        rental.DurationInDays,
        rental.DailyRateSnapshot.Amount,
        rental.TotalAmount.Amount,
        rental.LateFee.Amount,
        rental.AmountDue.Amount,
        rental.Status.ToString());
}
