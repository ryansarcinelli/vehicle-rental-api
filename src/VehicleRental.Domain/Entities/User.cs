using VehicleRental.Domain.Common;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Domain.Entities;

public class User : Entity
{
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Customer? Customer { get; private set; }

    // EF Core
    private User()
    {
    }

    public static User Create(string email, string passwordHash, UserRole role, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("O hash da senha é obrigatório.");

        if (!Enum.IsDefined(role))
            throw new DomainException($"Perfil inválido: {(int)role}.");

        return new User
        {
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// E-mail é a chave de login, então é guardado sempre em minúsculo e sem espaços.
    /// </summary>
    public static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("O e-mail é obrigatório.");

        var normalized = email.Trim().ToLowerInvariant();

        if (!normalized.Contains('@') || normalized.StartsWith('@') || normalized.EndsWith('@'))
            throw new DomainException($"E-mail inválido: '{email}'.");

        return normalized;
    }
}
