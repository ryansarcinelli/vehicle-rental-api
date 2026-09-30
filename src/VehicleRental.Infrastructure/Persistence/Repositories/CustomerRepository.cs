using Microsoft.EntityFrameworkCore;
using VehicleRental.Application.Abstractions;
using VehicleRental.Domain.Entities;

namespace VehicleRental.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(AppDbContext context) : ICustomerRepository
{
    public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<bool> DriverLicenseExistsAsync(
        string driverLicense,
        Guid? excludeCustomerId = null,
        CancellationToken cancellationToken = default)
        => context.Customers
            .Where(c => c.DriverLicense == driverLicense)
            .Where(c => excludeCustomerId == null || c.Id != excludeCustomerId)
            .AnyAsync(cancellationToken);

    public void Add(Customer customer) => context.Customers.Add(customer);
}
