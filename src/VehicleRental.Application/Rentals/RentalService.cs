using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Rentals.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Application.Rentals;

public sealed class RentalService(
    IRentalRepository rentals,
    IVehicleRepository vehicles,
    ICustomerRepository customers,
    IDateTimeProvider clock,
    IUnitOfWork unitOfWork)
{
    public async Task<RentalResponse> CreateAsync(
        CurrentUser currentUser,
        CreateRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerProfile(currentUser, cancellationToken);

        if (await rentals.HasActiveRentalAsync(customer.Id, cancellationToken))
            throw new DomainException("O cliente já possui um aluguel ativo.");

        var vehicle = await vehicles.GetByIdAsync(request.VehicleId, cancellationToken)
                      ?? throw new NotFoundException("Veículo", request.VehicleId);

        var rental = Rental.Create(
            vehicle,
            customer.Id,
            request.StartDate,
            request.EndDate,
            clock.Today,
            clock.UtcNow);

        rentals.Add(rental);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RentalResponse.From(rental);
    }

    /// <summary>
    /// Regra 10: Admin vê todos os aluguéis; Customer vê apenas os próprios.
    /// </summary>
    public async Task<IReadOnlyList<RentalResponse>> ListAsync(
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        Guid? customerFilter = null;

        if (!currentUser.IsAdmin)
        {
            var customer = await RequireCustomerProfile(currentUser, cancellationToken);
            customerFilter = customer.Id;
        }

        var result = await rentals.ListAsync(customerFilter, cancellationToken);
        return [.. result.Select(RentalResponse.From)];
    }

    public async Task<RentalResponse> GetByIdAsync(
        CurrentUser currentUser,
        Guid rentalId,
        CancellationToken cancellationToken = default)
    {
        var rental = await RequireRental(rentalId, cancellationToken);
        await EnsureCanAccess(currentUser, rental, cancellationToken);

        return RentalResponse.From(rental);
    }

    /// <summary>
    /// Devolução é operação de balcão: apenas Admin.
    /// </summary>
    public async Task<RentalResponse> ReturnAsync(
        CurrentUser currentUser,
        Guid rentalId,
        ReturnRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAdmin)
            throw new ForbiddenException("Apenas administradores podem registrar a devolução.");

        var rental = await RequireRental(rentalId, cancellationToken);
        var vehicle = await RequireVehicle(rental.VehicleId, cancellationToken);

        rental.Return(vehicle, request.ReturnDate ?? clock.Today);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RentalResponse.From(rental);
    }

    public async Task<RentalResponse> CancelAsync(
        CurrentUser currentUser,
        Guid rentalId,
        CancellationToken cancellationToken = default)
    {
        var rental = await RequireRental(rentalId, cancellationToken);
        await EnsureCanAccess(currentUser, rental, cancellationToken);

        var vehicle = await RequireVehicle(rental.VehicleId, cancellationToken);

        rental.Cancel(vehicle, clock.Today);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RentalResponse.From(rental);
    }

    private async Task<Rental> RequireRental(Guid rentalId, CancellationToken cancellationToken)
        => await rentals.GetByIdAsync(rentalId, cancellationToken)
           ?? throw new NotFoundException("Aluguel", rentalId);

    private async Task<Vehicle> RequireVehicle(Guid vehicleId, CancellationToken cancellationToken)
        => await vehicles.GetByIdAsync(vehicleId, cancellationToken)
           ?? throw new NotFoundException("Veículo", vehicleId);

    private async Task<Customer> RequireCustomerProfile(CurrentUser currentUser, CancellationToken cancellationToken)
        => await customers.GetByUserIdAsync(currentUser.UserId, cancellationToken)
           ?? throw new ForbiddenException("O usuário autenticado não possui perfil de cliente.");

    private async Task EnsureCanAccess(CurrentUser currentUser, Rental rental, CancellationToken cancellationToken)
    {
        if (currentUser.IsAdmin)
            return;

        var customer = await RequireCustomerProfile(currentUser, cancellationToken);

        if (!rental.BelongsTo(customer.Id))
            throw new ForbiddenException("Este aluguel pertence a outro cliente.");
    }
}
