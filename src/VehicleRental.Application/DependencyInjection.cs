using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using VehicleRental.Application.Auth;
using VehicleRental.Application.Customers;
using VehicleRental.Application.Rentals;
using VehicleRental.Application.Vehicles;

namespace VehicleRental.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<RentalService>();
        services.AddScoped<CustomerService>();

        services.AddValidatorsFromAssemblyContaining<AuthService>(ServiceLifetime.Singleton);

        return services;
    }
}
