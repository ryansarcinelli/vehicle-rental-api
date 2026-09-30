using FluentAssertions;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.Domain.ValueObjects;
using VehicleRental.UnitTests.TestSupport;

namespace VehicleRental.UnitTests.Domain;

public class RentalTests
{
    private static readonly DateOnly Today = TestData.Today;
    private static readonly DateTime Now = FixedDateTimeProvider.DefaultInstant;

    private static Rental CreateRental(Vehicle vehicle, int days = 3, int startOffset = 0)
        => Rental.Create(
            vehicle,
            Guid.NewGuid(),
            Today.AddDays(startOffset),
            Today.AddDays(startOffset + days),
            Today,
            Now);

    // ---------- criação ----------

    [Fact]
    public void Create_NasceAtivoEAlugaOVeiculo()
    {
        var vehicle = TestData.AVehicle();

        var rental = CreateRental(vehicle);

        rental.Status.Should().Be(RentalStatus.Active);
        rental.LateFee.Amount.Should().Be(0m);
        vehicle.Status.Should().Be(VehicleStatus.Rented);
    }

    [Fact]
    public void Create_CongelaADiariaDoVeiculo()
    {
        var vehicle = TestData.AVehicle(dailyRate: 150m);

        var rental = CreateRental(vehicle);
        vehicle.UpdateDetails("ABC1D23", "VW", "Gol", 2024, VehicleCategory.Hatch, 999m, Now.Year);

        rental.DailyRateSnapshot.Amount.Should().Be(150m);
        rental.TotalAmount.Amount.Should().Be(450m);
    }

    [Fact]
    public void Create_ComVeiculoIndisponivel_Lanca()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();

        FluentActions.Invoking(() => CreateRental(vehicle))
            .Should().Throw<DomainException>()
            .WithMessage("*não está disponível*");
    }

    [Fact]
    public void Create_ComDevolucaoAntesDaRetirada_Lanca()
        => FluentActions
            .Invoking(() => Rental.Create(TestData.AVehicle(), Guid.NewGuid(), Today.AddDays(5), Today.AddDays(2), Today, Now))
            .Should().Throw<DomainException>()
            .WithMessage("*posterior à data de retirada*");

    [Fact]
    public void Create_ComPeriodoDeZeroDias_Lanca()
        => FluentActions
            .Invoking(() => Rental.Create(TestData.AVehicle(), Guid.NewGuid(), Today, Today, Today, Now))
            .Should().Throw<DomainException>()
            .WithMessage("*posterior à data de retirada*");

    [Fact]
    public void Create_ComRetiradaNoPassado_Lanca()
        => FluentActions.Invoking(() => CreateRental(TestData.AVehicle(), startOffset: -1))
            .Should().Throw<DomainException>()
            .WithMessage("*não pode estar no passado*");

    [Fact]
    public void Create_ComPeriodoAcimaDoMaximo_Lanca()
        => FluentActions.Invoking(() => CreateRental(TestData.AVehicle(), days: Rental.MaxDurationInDays + 1))
            .Should().Throw<DomainException>()
            .WithMessage("*período máximo*");

    [Fact]
    public void Create_ComPeriodoExatamenteNoMaximo_Aceita()
        => CreateRental(TestData.AVehicle(), days: Rental.MaxDurationInDays)
            .DurationInDays.Should().Be(Rental.MaxDurationInDays);

    [Fact]
    public void Create_SemCliente_Lanca()
        => FluentActions
            .Invoking(() => Rental.Create(TestData.AVehicle(), Guid.Empty, Today, Today.AddDays(2), Today, Now))
            .Should().Throw<DomainException>()
            .WithMessage("*cliente é obrigatório*");

    // ---------- cálculo do total (regra 5) ----------

    [Theory]
    [InlineData(1, 100)]      // sem desconto
    [InlineData(6, 600)]      // último dia sem desconto
    [InlineData(7, 630)]      // 700 - 10%
    [InlineData(29, 2610)]    // 2900 - 10%
    [InlineData(30, 2550)]    // 3000 - 15%
    [InlineData(90, 7650)]    // 9000 - 15%
    public void CalculateTotal_AplicaODescontoPorFaixaDeDias(int days, decimal expected)
        => Rental.CalculateTotal(Money.Create(100m), days).Amount.Should().Be(expected);

    [Fact]
    public void CalculateTotal_ArredondaODescontoParaDuasCasas()
        => Rental.CalculateTotal(Money.Create(135.50m), 7).Amount.Should().Be(853.65m);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void CalculateTotal_ComPeriodoNaoPositivo_Lanca(int days)
        => FluentActions.Invoking(() => Rental.CalculateTotal(Money.Create(100m), days))
            .Should().Throw<DomainException>()
            .WithMessage("*pelo menos um dia*");

    [Fact]
    public void Create_UsaOMesmoCalculoDeTotal()
        => CreateRental(TestData.AVehicle(dailyRate: 100m), days: 10)
            .TotalAmount.Amount.Should().Be(900m); // 1000 - 10%

    // ---------- devolução (regra 6) ----------

    [Fact]
    public void Return_NoPrazo_NaoCobraMultaEEncerraOAluguel()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle, days: 3);

        rental.Return(vehicle, rental.EndDate);

        rental.Status.Should().Be(RentalStatus.Completed);
        rental.ReturnedAt.Should().Be(rental.EndDate);
        rental.LateFee.Amount.Should().Be(0m);
        rental.AmountDue.Should().Be(rental.TotalAmount);
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void Return_Antecipada_NaoCobraMulta()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle, days: 5);

        rental.Return(vehicle, rental.StartDate.AddDays(2));

        rental.LateFee.Amount.Should().Be(0m);
        rental.Status.Should().Be(RentalStatus.Completed);
    }

    [Theory]
    [InlineData(1, 150)]    // 1 x 100 x 1.5
    [InlineData(3, 450)]
    [InlineData(10, 1500)]
    public void Return_ComAtraso_CobraDiariaVezes1e5PorDia(int lateDays, decimal expectedFee)
    {
        var vehicle = TestData.AVehicle(dailyRate: 100m);
        var rental = CreateRental(vehicle, days: 3);

        rental.Return(vehicle, rental.EndDate.AddDays(lateDays));

        rental.LateFee.Amount.Should().Be(expectedFee);
        rental.AmountDue.Amount.Should().Be(rental.TotalAmount.Amount + expectedFee);
    }

    [Fact]
    public void Return_ComAtraso_UsaADiariaCongeladaENaoAAtual()
    {
        var vehicle = TestData.AVehicle(dailyRate: 100m);
        var rental = CreateRental(vehicle, days: 3);
        vehicle.UpdateDetails("ABC1D23", "VW", "Gol", 2024, VehicleCategory.Hatch, 900m, Now.Year);

        rental.Return(vehicle, rental.EndDate.AddDays(2));

        rental.LateFee.Amount.Should().Be(300m);
    }

    [Fact]
    public void Return_DuasVezes_Lanca()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle);
        rental.Return(vehicle, rental.EndDate);

        FluentActions.Invoking(() => rental.Return(vehicle, rental.EndDate))
            .Should().Throw<DomainException>()
            .WithMessage("*Somente aluguéis ativos*");
    }

    [Fact]
    public void Return_AntesDaRetirada_Lanca()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle, startOffset: 2);

        FluentActions.Invoking(() => rental.Return(vehicle, rental.StartDate.AddDays(-1)))
            .Should().Throw<DomainException>()
            .WithMessage("*anterior à data de retirada*");
    }

    [Fact]
    public void Return_ComOutroVeiculo_Lanca()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle);
        var outroVeiculo = TestData.AVehicle(plate: "XYZ9Z99");

        FluentActions.Invoking(() => rental.Return(outroVeiculo, rental.EndDate))
            .Should().Throw<DomainException>()
            .WithMessage("*não corresponde*");
    }

    // ---------- cancelamento ----------

    [Fact]
    public void Cancel_AntesDaRetirada_LiberaOVeiculo()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle, startOffset: 2);

        rental.Cancel(vehicle, Today);

        rental.Status.Should().Be(RentalStatus.Cancelled);
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void Cancel_NoDiaDaRetirada_Lanca()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle);

        FluentActions.Invoking(() => rental.Cancel(vehicle, Today))
            .Should().Throw<DomainException>()
            .WithMessage("*já foi iniciado*");
    }

    [Fact]
    public void Cancel_DepoisDeDevolvido_Lanca()
    {
        var vehicle = TestData.AVehicle();
        var rental = CreateRental(vehicle);
        rental.Return(vehicle, rental.EndDate);

        FluentActions.Invoking(() => rental.Cancel(vehicle, Today.AddDays(-1)))
            .Should().Throw<DomainException>()
            .WithMessage("*Somente aluguéis ativos*");
    }

    // ---------- propriedade (regra 10) ----------

    [Fact]
    public void BelongsTo_ReconheceApenasODonoDoAluguel()
    {
        var customerId = Guid.NewGuid();
        var rental = TestData.ARental(TestData.AVehicle(), customerId);

        rental.BelongsTo(customerId).Should().BeTrue();
        rental.BelongsTo(Guid.NewGuid()).Should().BeFalse();
    }
}
