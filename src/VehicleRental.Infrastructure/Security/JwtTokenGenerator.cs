using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VehicleRental.Application.Abstractions;
using VehicleRental.Domain.Entities;

namespace VehicleRental.Infrastructure.Security;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, IDateTimeProvider clock) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public GeneratedToken Generate(User user)
    {
        var expiresAt = clock.UtcNow.AddMinutes(_options.ExpirationInMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = clock.UtcNow,
            NotBefore = clock.UtcNow,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [ClaimTypes.Role] = user.Role.ToString()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new GeneratedToken(token, expiresAt);
    }
}
