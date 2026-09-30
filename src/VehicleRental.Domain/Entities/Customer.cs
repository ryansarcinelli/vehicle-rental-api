using VehicleRental.Domain.Common;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Domain.Entities;

public class Customer : Entity
{
    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string DriverLicense { get; private set; } = null!;
    public string Phone { get; private set; } = null!;

    public User? User { get; private set; }

    // EF Core
    private Customer()
    {
    }

    public static Customer Create(Guid userId, string fullName, string driverLicense, string phone)
    {
        if (userId == Guid.Empty)
            throw new DomainException("O usuário é obrigatório.");

        return new Customer
        {
            UserId = userId,
            FullName = RequireText(fullName, "nome"),
            DriverLicense = ValidDriverLicense(driverLicense),
            Phone = RequireText(phone, "telefone")
        };
    }

    public void UpdateProfile(string fullName, string driverLicense, string phone)
    {
        FullName = RequireText(fullName, "nome");
        DriverLicense = ValidDriverLicense(driverLicense);
        Phone = RequireText(phone, "telefone");
    }

    private static string RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"O campo '{field}' é obrigatório.");

        return value.Trim();
    }

    /// <summary>
    /// CNH brasileira: 11 dígitos.
    /// </summary>
    private static string ValidDriverLicense(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.Length != 11)
            throw new DomainException($"CNH inválida: '{value}'. Informe os 11 dígitos.");

        return digits;
    }
}
