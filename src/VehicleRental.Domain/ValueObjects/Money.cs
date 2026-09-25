using VehicleRental.Domain.Exceptions;

namespace VehicleRental.Domain.ValueObjects;

/// <summary>
/// Valor monetário não negativo com 2 casas decimais.
/// Existe para que nenhum preço, total ou multa possa ficar negativo por acidente.
/// </summary>
public sealed class Money : IEquatable<Money>, IComparable<Money>
{
    public static readonly Money Zero = new(0m);

    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Money Create(decimal amount)
    {
        if (amount < 0)
            throw new DomainException($"Valor monetário não pode ser negativo: {amount}.");

        return new Money(Math.Round(amount, 2, MidpointRounding.AwayFromZero));
    }

    public Money Add(Money other) => Create(Amount + other.Amount);

    public Money Multiply(decimal factor) => Create(Amount * factor);

    public Money ApplyDiscount(decimal percentage)
    {
        if (percentage is < 0 or > 100)
            throw new DomainException($"Percentual de desconto inválido: {percentage}.");

        return Create(Amount * (1 - percentage / 100m));
    }

    public int CompareTo(Money? other) => other is null ? 1 : Amount.CompareTo(other.Amount);

    // Os operadores de ordem existem para o domínio comparar valores sem desembrulhar o VO,
    // e é o que permite ao EF Core traduzir filtros de faixa de preço para SQL.
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public bool Equals(Money? other) => other is not null && other.Amount == Amount;

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => Amount.GetHashCode();

    public override string ToString() => Amount.ToString("0.00");
}
