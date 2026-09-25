using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Auth.Dtos;
using VehicleRental.Application.Common;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Application.Auth;

public sealed class AuthService(
    IUserRepository users,
    ICustomerRepository customers,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    IDateTimeProvider clock,
    IUnitOfWork unitOfWork)
{
    public const int MinimumPasswordLength = 8;

    /// <summary>
    /// Cadastro público, sempre no perfil Customer. Admin vem do seed.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = User.NormalizeEmail(request.Email);

        if (request.Password?.Length is null or < MinimumPasswordLength)
            throw new DomainException($"A senha deve ter no mínimo {MinimumPasswordLength} caracteres.");

        if (await users.EmailExistsAsync(email, cancellationToken))
            throw new DomainException($"O e-mail '{email}' já está cadastrado.");

        var user = User.Create(email, passwordHasher.Hash(request.Password), UserRole.Customer, clock.UtcNow);
        var customer = Customer.Create(user.Id, request.FullName, request.DriverLicense, request.Phone);

        if (await customers.DriverLicenseExistsAsync(customer.DriverLicense, cancellationToken: cancellationToken))
            throw new DomainException($"A CNH '{customer.DriverLicense}' já está cadastrada.");

        users.Add(user);
        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Não usa User.NormalizeEmail aqui: um e-mail mal formatado no login é credencial
        // inválida, não erro de validação de domínio.
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

        var user = await users.GetByEmailAsync(email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            throw new InvalidCredentialsException();

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var token = tokenGenerator.Generate(user);
        return new AuthResponse(token.Token, token.ExpiresAtUtc, user.Email, user.Role.ToString());
    }
}
