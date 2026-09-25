using FluentAssertions;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.UnitTests.TestSupport;

namespace VehicleRental.UnitTests.Domain;

public class VehicleTests
{
    private const int CurrentYear = 2026;

    [Fact]
    public void Create_NasceDisponivel()
        => TestData.AVehicle().Status.Should().Be(VehicleStatus.Available);

    [Fact]
    public void Create_NormalizaAPlacaEFazTrimDosTextos()
    {
        var vehicle = TestData.AVehicle(plate: "abc-1d23", brand: "  Volkswagen  ", model: " Gol ");

        vehicle.Plate.Value.Should().Be("ABC1D23");
        vehicle.Brand.Should().Be("Volkswagen");
        vehicle.Model.Should().Be("Gol");
    }

    [Theory]
    [InlineData(1900)]
    [InlineData(CurrentYear)]
    [InlineData(CurrentYear + 1)]
    public void Create_ComAnoNaFaixaPermitida_Aceita(int year)
        => TestData.AVehicle(year: year).Year.Should().Be(year);

    [Theory]
    [InlineData(1899)]
    [InlineData(CurrentYear + 2)]
    [InlineData(0)]
    public void Create_ComAnoForaDaFaixa_Lanca(int year)
        => FluentActions.Invoking(() => TestData.AVehicle(year: year))
            .Should().Throw<DomainException>()
            .WithMessage("*Ano inválido*");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SemMarca_Lanca(string brand)
        => FluentActions.Invoking(() => TestData.AVehicle(brand: brand))
            .Should().Throw<DomainException>()
            .WithMessage("*obrigatório*");

    [Fact]
    public void Create_ComDiariaNegativa_Lanca()
        => FluentActions.Invoking(() => TestData.AVehicle(dailyRate: -1m))
            .Should().Throw<DomainException>()
            .WithMessage("*não pode ser negativo*");

    [Fact]
    public void Create_ComCategoriaInexistente_Lanca()
        => FluentActions.Invoking(() => TestData.AVehicle(category: (VehicleCategory)99))
            .Should().Throw<DomainException>()
            .WithMessage("*Categoria inválida*");

    [Fact]
    public void MarkAsRented_QuandoDisponivel_MudaParaAlugado()
    {
        var vehicle = TestData.AVehicle();

        vehicle.MarkAsRented();

        vehicle.Status.Should().Be(VehicleStatus.Rented);
    }

    [Fact]
    public void MarkAsRented_QuandoJaAlugado_Lanca()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();

        FluentActions.Invoking(vehicle.MarkAsRented)
            .Should().Throw<DomainException>()
            .WithMessage("*não está disponível*");
    }

    [Fact]
    public void MarkAsRented_QuandoEmManutencao_Lanca()
    {
        var vehicle = TestData.AVehicle();
        vehicle.SendToMaintenance();

        FluentActions.Invoking(vehicle.MarkAsRented)
            .Should().Throw<DomainException>()
            .WithMessage("*não está disponível*");
    }

    [Fact]
    public void MarkAsAvailable_DepoisDeAlugado_VoltaParaDisponivel()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();

        vehicle.MarkAsAvailable();

        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void MarkAsAvailable_QuandoJaDisponivel_Lanca()
        => FluentActions.Invoking(TestData.AVehicle().MarkAsAvailable)
            .Should().Throw<DomainException>()
            .WithMessage("*já está disponível*");

    [Fact]
    public void SendToMaintenance_QuandoAlugado_Lanca()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();

        FluentActions.Invoking(vehicle.SendToMaintenance)
            .Should().Throw<DomainException>()
            .WithMessage("*está alugado*");
    }

    [Fact]
    public void EnsureCanBeDeleted_QuandoAlugado_Lanca()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();

        FluentActions.Invoking(vehicle.EnsureCanBeDeleted)
            .Should().Throw<DomainException>()
            .WithMessage("*não pode ser excluído*");
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Maintenance)]
    public void EnsureCanBeDeleted_QuandoNaoAlugado_NaoLanca(VehicleStatus status)
    {
        var vehicle = TestData.AVehicle();

        if (status == VehicleStatus.Maintenance)
            vehicle.SendToMaintenance();

        FluentActions.Invoking(vehicle.EnsureCanBeDeleted).Should().NotThrow();
    }

    [Fact]
    public void UpdateDetails_TrocaOsDadosMasPreservaOStatus()
    {
        var vehicle = TestData.AVehicle();
        vehicle.SendToMaintenance();

        vehicle.UpdateDetails("XYZ9Z99", "Toyota", "Corolla", 2025, VehicleCategory.Sedan, 210m, CurrentYear);

        vehicle.Plate.Value.Should().Be("XYZ9Z99");
        vehicle.Brand.Should().Be("Toyota");
        vehicle.DailyRate.Amount.Should().Be(210m);
        vehicle.Status.Should().Be(VehicleStatus.Maintenance);
    }

    [Fact]
    public void Veiculos_SaoComparadosPorId()
    {
        var vehicle = TestData.AVehicle();

        vehicle.Should().Be(vehicle);
        vehicle.Should().NotBe(TestData.AVehicle(plate: "XYZ9Z99"));
    }
}
