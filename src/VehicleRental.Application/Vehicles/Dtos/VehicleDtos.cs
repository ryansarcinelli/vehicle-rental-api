using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;

namespace VehicleRental.Application.Vehicles.Dtos;

public sealed record CreateVehicleRequest(
    string Plate,
    string Brand,
    string Model,
    int Year,
    VehicleCategory Category,
    decimal DailyRate);

public sealed record UpdateVehicleRequest(
    string Plate,
    string Brand,
    string Model,
    int Year,
    VehicleCategory Category,
    decimal DailyRate);

public sealed record ChangeVehicleStatusRequest(VehicleStatus Status);

public sealed record VehicleResponse(
    Guid Id,
    string Plate,
    string Brand,
    string Model,
    int Year,
    string Category,
    decimal DailyRate,
    string Status)
{
    public static VehicleResponse From(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.Plate.Value,
        vehicle.Brand,
        vehicle.Model,
        vehicle.Year,
        vehicle.Category.ToString(),
        vehicle.DailyRate.Amount,
        vehicle.Status.ToString());
}

/// <summary>
/// Filtros e paginação da listagem de veículos.
/// </summary>
public sealed record VehicleQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public VehicleCategory? Category { get; init; }

    public VehicleStatus? Status { get; init; }

    public string? Brand { get; init; }

    public decimal? MaxDailyRate { get; init; }

    /// <summary>
    /// Mantém os limites saudáveis mesmo se a query string vier com valores absurdos.
    /// </summary>
    public VehicleQuery Normalized() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => PageSize
        },
        Brand = string.IsNullOrWhiteSpace(Brand) ? null : Brand.Trim()
    };
}
