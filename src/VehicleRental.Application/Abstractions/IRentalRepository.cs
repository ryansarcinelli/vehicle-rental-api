using VehicleRental.Domain.Entities;

namespace VehicleRental.Application.Abstractions;

public interface IRentalRepository
{
    Task<Rental?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regra 9: um cliente não pode ter dois aluguéis ativos ao mesmo tempo.
    /// </summary>
    Task<bool> HasActiveRentalAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Regra 8: um veículo com histórico de aluguéis não pode ser excluído, senão a
    /// exclusão apagaria registros financeiros — e a FK do banco recusaria de todo jeito.
    /// </summary>
    Task<bool> HasAnyForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Rental>> ListAsync(Guid? customerId, CancellationToken cancellationToken = default);

    void Add(Rental rental);
}
