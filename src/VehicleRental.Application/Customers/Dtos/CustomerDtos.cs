using VehicleRental.Domain.Entities;

namespace VehicleRental.Application.Customers.Dtos;

public sealed record UpdateCustomerRequest(
    string FullName,
    string DriverLicense,
    string Phone);

public sealed record CustomerResponse(
    Guid Id,
    string Email,
    string FullName,
    string DriverLicense,
    string Phone)
{
    public static CustomerResponse From(Customer customer) => new(
        customer.Id,
        customer.User?.Email ?? string.Empty,
        customer.FullName,
        customer.DriverLicense,
        customer.Phone);
}
