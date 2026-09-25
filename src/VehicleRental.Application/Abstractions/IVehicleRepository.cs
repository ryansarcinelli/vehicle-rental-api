using VehicleRental.Application.Common;
using VehicleRental.Application.Vehicles.Dtos;
using VehicleRental.Domain.Entities;

namespace VehicleRental.Application.Abstractions;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> PlateExistsAsync(string plate, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default);

    Task<PagedResult<Vehicle>> SearchAsync(VehicleQuery query, CancellationToken cancellationToken = default);

    void Add(Vehicle vehicle);

    void Remove(Vehicle vehicle);
}
