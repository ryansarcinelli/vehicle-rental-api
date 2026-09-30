using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VehicleRental.Application.Abstractions;
using VehicleRental.Infrastructure.Persistence;
using VehicleRental.Infrastructure.Persistence.Repositories;
using VehicleRental.Infrastructure.Security;
using VehicleRental.Infrastructure.Time;

namespace VehicleRental.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Único ponto onde as interfaces da Application são ligadas às implementações
    /// concretas. Trocar Postgres por outro banco mexe só aqui.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' não configurada. Defina ConnectionStrings__Default.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IRentalRepository, RentalRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Secret), "Jwt:Secret é obrigatório.")
            .Validate(o => o.Secret.Length >= 32, "Jwt:Secret deve ter no mínimo 32 caracteres.")
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
