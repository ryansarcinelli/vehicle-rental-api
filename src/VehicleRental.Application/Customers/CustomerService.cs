using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Customers.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Application.Customers;

public sealed class CustomerService(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork)
{
    public async Task<CustomerResponse> GetMeAsync(CurrentUser currentUser, CancellationToken cancellationToken = default)
        => CustomerResponse.From(await RequireProfile(currentUser, cancellationToken));

    public async Task<CustomerResponse> UpdateMeAsync(
        CurrentUser currentUser,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireProfile(currentUser, cancellationToken);

        var digitsOnly = new string((request.DriverLicense ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digitsOnly != customer.DriverLicense
            && await customers.DriverLicenseExistsAsync(digitsOnly, customer.Id, cancellationToken))
        {
            throw new DomainException($"A CNH '{digitsOnly}' já está cadastrada para outro cliente.");
        }

        customer.UpdateProfile(request.FullName, digitsOnly, request.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerResponse.From(customer);
    }

    private async Task<Customer> RequireProfile(CurrentUser currentUser, CancellationToken cancellationToken)
        => await customers.GetByUserIdAsync(currentUser.UserId, cancellationToken)
           ?? throw new ForbiddenException("O usuário autenticado não possui perfil de cliente.");
}
