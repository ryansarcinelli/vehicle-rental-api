using System.Text.RegularExpressions;
using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Domain.ValueObjects;

/// <summary>
/// Placa de veículo brasileira, nos formatos Mercosul (ABC1D23) e antigo (ABC1234).
/// Value object: comparado por valor e imutável depois de criado.
/// </summary>
public sealed partial class Plate : IEquatable<Plate>
{
    public string Value { get; }

    private Plate(string value) => Value = value;

    public static Plate Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("A placa é obrigatória.");

        var normalized = NonAlphanumeric().Replace(input, string.Empty).ToUpperInvariant();

        if (!Mercosul().IsMatch(normalized) && !Legacy().IsMatch(normalized))
            throw new DomainException($"Placa inválida: '{input}'. Use o formato ABC1D23 ou ABC1234.");

        return new Plate(normalized);
    }

    public bool Equals(Plate? other) => other is not null && other.Value == Value;

    public override bool Equals(object? obj) => Equals(obj as Plate);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^[A-Z]{3}[0-9][A-Z][0-9]{2}$")]
    private static partial Regex Mercosul();

    [GeneratedRegex(@"^[A-Z]{3}[0-9]{4}$")]
    private static partial Regex Legacy();
}
