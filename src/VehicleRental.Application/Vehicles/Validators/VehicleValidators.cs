using FluentValidation;
using VehicleRental.Application.Vehicles.Dtos;

namespace VehicleRental.Application.Vehicles.Validators;

public sealed class CreateVehicleRequestValidator : AbstractValidator<CreateVehicleRequest>
{
    public CreateVehicleRequestValidator()
    {
        RuleFor(x => x.Plate).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Year).GreaterThan(1899);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DailyRate).GreaterThan(0);
    }
}

public sealed class UpdateVehicleRequestValidator : AbstractValidator<UpdateVehicleRequest>
{
    public UpdateVehicleRequestValidator()
    {
        RuleFor(x => x.Plate).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Year).GreaterThan(1899);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DailyRate).GreaterThan(0);
    }
}

public sealed class ChangeVehicleStatusRequestValidator : AbstractValidator<ChangeVehicleStatusRequest>
{
    public ChangeVehicleStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
