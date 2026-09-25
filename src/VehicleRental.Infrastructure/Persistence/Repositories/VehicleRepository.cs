using Microsoft.EntityFrameworkCore;
using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Vehicles.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository(AppDbContext context) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<bool> PlateExistsAsync(
        string plate,
        Guid? excludeVehicleId = null,
        CancellationToken cancellationToken = default)
    {
        // Compara o value object inteiro, não `v.Plate.Value`: a coluna é gerada por um
        // ValueConverter, então o EF não sabe traduzir acesso a membro interno do VO.
        var target = Plate.Create(plate);

        var query = context.Vehicles.Where(v => v.Plate == target);

        if (excludeVehicleId is { } excluded)
            query = query.Where(v => v.Id != excluded);

        return query.AnyAsync(cancellationToken);
    }

    public async Task<PagedResult<Vehicle>> SearchAsync(
        VehicleQuery query,
        CancellationToken cancellationToken = default)
    {
        var filtered = context.Vehicles.AsNoTracking();

        // Os filtros desempacotam o nullable para uma variável local: comparar direto com
        // `query.Category` geraria um cast para int? que o EF não traduz sobre uma coluna
        // convertida para texto.
        if (query.Category is { } category)
            filtered = filtered.Where(v => v.Category == category);

        if (query.Status is { } status)
            filtered = filtered.Where(v => v.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Brand))
            filtered = filtered.Where(v => EF.Functions.ILike(v.Brand, $"%{query.Brand}%"));

        if (query.MaxDailyRate is { } maxDailyRate)
        {
            // Compara Money com Money: o EF traduz a operação para a coluna numeric por trás
            // do converter. Alcançar `DailyRate.Amount` não seria traduzível.
            var ceiling = Money.Create(maxDailyRate);
            filtered = filtered.Where(v => v.DailyRate <= ceiling);
        }

        var total = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderBy(v => v.Brand)
            .ThenBy(v => v.Model)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Vehicle>(items, query.Page, query.PageSize, total);
    }

    public void Add(Vehicle vehicle) => context.Vehicles.Add(vehicle);

    public void Remove(Vehicle vehicle) => context.Vehicles.Remove(vehicle);
}
