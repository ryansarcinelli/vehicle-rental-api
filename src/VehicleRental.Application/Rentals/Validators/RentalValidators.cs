using FluentValidation;
using VehicleRental.Application.Rentals.Dtos;

namespace VehicleRental.Application.Rentals.Validators;

public sealed class CreateRentalRequestValidator : AbstractValidator<CreateRentalRequest>
{
    public CreateRentalRequestValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("A data de devolução deve ser posterior à data de retirada.");
    }
}
