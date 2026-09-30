using FluentValidation;
using VehicleRental.Application.Customers.Dtos;

namespace VehicleRental.Application.Customers.Validators;

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DriverLicense).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}
