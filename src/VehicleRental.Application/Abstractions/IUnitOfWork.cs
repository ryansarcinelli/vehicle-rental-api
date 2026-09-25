namespace VehicleRental.Application.Abstractions;

/// <summary>
/// Confirma as alterações rastreadas na transação atual. Separado dos repositórios para
/// que um caso de uso que altera duas entidades (aluguel + veículo) grave tudo de uma vez.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
