using FluentAssertions;
using VehicleRental.Domain.Exceptions;
using VehicleRental.Domain.ValueObjects;

namespace VehicleRental.UnitTests.Domain;

public class PlateTests
{
    [Theory]
    [InlineData("ABC1D23")]
    [InlineData("XYZ9Z99")]
    [InlineData("ABC1234")]
    public void Create_ComFormatoValido_AceitaAPlaca(string input)
        => Plate.Create(input).Value.Should().Be(input);

    [Theory]
    [InlineData("abc1d23", "ABC1D23")]
    [InlineData("ABC-1D23", "ABC1D23")]
    [InlineData(" abc 1234 ", "ABC1234")]
    public void Create_NormalizaCaixaEPontuacao(string input, string expected)
        => Plate.Create(input).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_SemValor_Lanca(string? input)
        => FluentActions.Invoking(() => Plate.Create(input))
            .Should().Throw<DomainException>()
            .WithMessage("*obrigatória*");

    [Theory]
    [InlineData("AB1234")]      // letras insuficientes
    [InlineData("ABCD123")]     // 4 letras
    [InlineData("ABC12D3")]     // dígito e letra invertidos
    [InlineData("ABC1D234")]    // longa demais
    [InlineData("1234567")]     // só dígitos
    public void Create_ComFormatoInvalido_Lanca(string input)
        => FluentActions.Invoking(() => Plate.Create(input))
            .Should().Throw<DomainException>()
            .WithMessage("*Placa inválida*");

    [Fact]
    public void Placas_SaoComparadasPorValor()
    {
        var mercosul = Plate.Create("ABC1D23");
        var mesmaPlacaEscritaDeOutroJeito = Plate.Create("abc-1d23");

        mercosul.Should().Be(mesmaPlacaEscritaDeOutroJeito);
        mercosul.GetHashCode().Should().Be(mesmaPlacaEscritaDeOutroJeito.GetHashCode());
    }

    [Fact]
    public void PlacasDiferentes_NaoSaoIguais()
        => Plate.Create("ABC1D23").Should().NotBe(Plate.Create("XYZ9Z99"));
}
