using VehicleRental.Domain.Entities;

namespace VehicleRental.Application.Abstractions;

public interface IJwtTokenGenerator
{
    GeneratedToken Generate(User user);
}

public sealed record GeneratedToken(string Token, DateTime ExpiresAtUtc);
