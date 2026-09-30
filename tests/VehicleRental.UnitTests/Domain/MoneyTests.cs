using FluentAssertions;
using VehicleRental.Domain.Exceptions;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Create_ComValorPositivo_GuardaOValor()
        => Money.Create(199.90m).Amount.Should().Be(199.90m);

    [Fact]
    public void Create_ComZero_EhPermitido()
        => Money.Create(0m).Amount.Should().Be(0m);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_ComValorNegativo_Lanca(decimal amount)
        => FluentActions.Invoking(() => Money.Create(amount))
            .Should().Throw<DomainException>()
            .WithMessage("*não pode ser negativo*");

    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(0.125, 0.13)]
    public void Create_ArredondaParaDuasCasas(decimal input, decimal expected)
        => Money.Create(input).Amount.Should().Be(expected);

    [Fact]
    public void Multiply_MultiplicaEArredonda()
        => Money.Create(33.33m).Multiply(3).Amount.Should().Be(99.99m);

    [Fact]
    public void Add_SomaOsValores()
        => Money.Create(10.50m).Add(Money.Create(4.50m)).Amount.Should().Be(15.00m);

    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(100, 15, 85)]
    [InlineData(100, 0, 100)]
    [InlineData(333.33, 10, 300.00)]
    public void ApplyDiscount_AplicaOPercentual(decimal amount, decimal percentage, decimal expected)
        => Money.Create(amount).ApplyDiscount(percentage).Amount.Should().Be(expected);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ApplyDiscount_ComPercentualForaDaFaixa_Lanca(decimal percentage)
        => FluentActions.Invoking(() => Money.Create(100m).ApplyDiscount(percentage))
            .Should().Throw<DomainException>()
            .WithMessage("*desconto inválido*");

    [Fact]
    public void Valores_SaoComparadosPorValor()
        => Money.Create(50m).Should().Be(Money.Create(50m));

    [Fact]
    public void OperadoresDeOrdem_ComparamPeloValor()
    {
        var menor = Money.Create(10m);
        var maior = Money.Create(20m);

        (menor < maior).Should().BeTrue();
        (maior > menor).Should().BeTrue();
        (menor <= Money.Create(10m)).Should().BeTrue();
        (maior >= Money.Create(20m)).Should().BeTrue();
        (maior < menor).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_OrdenaUmaColecaoDeValores()
    {
        var valores = new[] { Money.Create(30m), Money.Create(10m), Money.Create(20m) };

        valores.Order().Select(m => m.Amount).Should().ContainInOrder(10m, 20m, 30m);
    }
}
