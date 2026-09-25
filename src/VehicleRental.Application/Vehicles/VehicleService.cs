using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Vehicles.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.Application.Vehicles;

public sealed class VehicleService(
    IVehicleRepository vehicles,
    IRentalRepository rentals,
    IDateTimeProvider clock,
    IUnitOfWork unitOfWork)
{
    public async Task<VehicleResponse> CreateAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlateIsFree(request.Plate, null, cancellationToken);

        var vehicle = Vehicle.Create(
            request.Plate,
            request.Brand,
            request.Model,
            request.Year,
            request.Category,
            request.DailyRate,
            clock.CurrentYear,
            clock.UtcNow);

        vehicles.Add(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return VehicleResponse.From(vehicle);
    }

    public async Task<VehicleResponse> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireVehicle(id, cancellationToken);

        await EnsurePlateIsFree(request.Plate, id, cancellationToken);

        vehicle.UpdateDetails(
            request.Plate,
            request.Brand,
            request.Model,
            request.Year,
            request.Category,
            request.DailyRate,
            clock.CurrentYear);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return VehicleResponse.From(vehicle);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireVehicle(id, cancellationToken);

        vehicle.EnsureCanBeDeleted();

        // Histórico de aluguéis é registro financeiro: o veículo sai de circulação por
        // manutenção, não por exclusão. Sem esta checagem a FK do banco responderia 500.
        if (await rentals.HasAnyForVehicleAsync(id, cancellationToken))
        {
            throw new DomainException(
                $"Veículo {vehicle.Plate} possui histórico de aluguéis e não pode ser excluído; "
                + "envie-o para manutenção.");
        }

        vehicles.Remove(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Só transições operadas por pessoas: enviar para manutenção e liberar de volta.
    /// O status Rented é consequência de um aluguel, nunca definido à mão.
    /// </summary>
    public async Task<VehicleResponse> ChangeStatusAsync(Guid id, VehicleStatus status, CancellationToken cancellationToken = default)
    {
        var vehicle = await RequireVehicle(id, cancellationToken);

        switch (status)
        {
            case VehicleStatus.Maintenance:
                vehicle.SendToMaintenance();
                break;

            case VehicleStatus.Available:
                vehicle.MarkAsAvailable();
                break;

            default:
                throw new DomainException(
                    $"O status '{status}' não pode ser definido manualmente; ele decorre de um aluguel.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return VehicleResponse.From(vehicle);
    }

    public async Task<VehicleResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => VehicleResponse.From(await RequireVehicle(id, cancellationToken));

    public async Task<PagedResult<VehicleResponse>> SearchAsync(VehicleQuery query, CancellationToken cancellationToken = default)
    {
        var page = await vehicles.SearchAsync(query.Normalized(), cancellationToken);
        return page.Map(VehicleResponse.From);
    }

    private async Task<Vehicle> RequireVehicle(Guid id, CancellationToken cancellationToken)
        => await vehicles.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException("Veículo", id);

    private async Task EnsurePlateIsFree(string plate, Guid? excludeVehicleId, CancellationToken cancellationToken)
    {
        // Normaliza antes de comparar: 'abc-1d23' e 'ABC1D23' são a mesma placa.
        var normalized = Plate.Create(plate).Value;

        if (await vehicles.PlateExistsAsync(normalized, excludeVehicleId, cancellationToken))
            throw new DomainException($"A placa '{normalized}' já está cadastrada.");
    }
}
