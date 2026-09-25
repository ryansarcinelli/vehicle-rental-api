using VehicleRental.Domain.Entities;

namespace VehicleRental.Application.Abstractions;

public interface ICustomerRepository
{
    Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DriverLicenseExistsAsync(string driverLicense, Guid? excludeCustomerId = null, CancellationToken cancellationToken = default);

    void Add(Customer customer);
}
