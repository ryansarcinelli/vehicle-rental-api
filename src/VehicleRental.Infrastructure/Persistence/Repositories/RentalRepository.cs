using Microsoft.EntityFrameworkCore;
using VehicleRental.Application.Abstractions;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;

namespace VehicleRental.Infrastructure.Persistence.Repositories;

public sealed class RentalRepository(AppDbContext context) : IRentalRepository
{
    public Task<Rental?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Rentals
            .Include(r => r.Vehicle)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> HasActiveRentalAsync(Guid customerId, CancellationToken cancellationToken = default)
        => context.Rentals
            .AnyAsync(r => r.CustomerId == customerId && r.Status == RentalStatus.Active, cancellationToken);

    public Task<bool> HasAnyForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
        => context.Rentals.AnyAsync(r => r.VehicleId == vehicleId, cancellationToken);

    public async Task<IReadOnlyList<Rental>> ListAsync(
        Guid? customerId,
        CancellationToken cancellationToken = default)
    {
        var query = context.Rentals
            .AsNoTracking()
            .Include(r => r.Vehicle)
            .Include(r => r.Customer)
            .AsQueryable();

        if (customerId is not null)
            query = query.Where(r => r.CustomerId == customerId);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(Rental rental) => context.Rentals.Add(rental);
}
