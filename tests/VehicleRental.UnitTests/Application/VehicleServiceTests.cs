using FluentAssertions;
using NSubstitute;
using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Vehicles;
using VehicleRental.Application.Vehicles.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.UnitTests.TestSupport;

namespace VehicleRental.UnitTests.Application;

public class VehicleServiceTests
{
    private readonly IVehicleRepository _vehicles = Substitute.For<IVehicleRepository>();
    private readonly IRentalRepository _rentals = Substitute.For<IRentalRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FixedDateTimeProvider _clock = new();

    private readonly VehicleService _sut;

    public VehicleServiceTests() => _sut = new VehicleService(_vehicles, _rentals, _clock, _unitOfWork);

    private static CreateVehicleRequest ValidCreate(string plate = "abc-1d23", decimal dailyRate = 120m)
        => new(plate, "Volkswagen", "Gol", 2024, VehicleCategory.Hatch, dailyRate);

    // ---------- criação ----------

    [Fact]
    public async Task Create_ComDadosValidos_AdicionaEGrava()
    {
        var response = await _sut.CreateAsync(ValidCreate());

        response.Plate.Should().Be("ABC1D23");
        response.Status.Should().Be(nameof(VehicleStatus.Available));
        response.DailyRate.Should().Be(120m);

        _vehicles.Received(1).Add(Arg.Any<Vehicle>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ComparaAPlacaJaNormalizada()
    {
        await _sut.CreateAsync(ValidCreate(plate: "abc-1d23"));

        // O repositório precisa receber 'ABC1D23', senão a checagem de duplicidade
        // deixaria passar a mesma placa escrita de outro jeito.
        await _vehicles.Received(1).PlateExistsAsync("ABC1D23", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ComPlacaDuplicada_LancaENaoGrava()
    {
        _vehicles.PlateExistsAsync("ABC1D23", null, Arg.Any<CancellationToken>()).Returns(true);

        await FluentActions.Awaiting(() => _sut.CreateAsync(ValidCreate()))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*já está cadastrada*");

        _vehicles.DidNotReceive().Add(Arg.Any<Vehicle>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ComPlacaInvalida_Lanca()
        => await FluentActions.Awaiting(() => _sut.CreateAsync(ValidCreate(plate: "1234567")))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*Placa inválida*");

    // ---------- atualização ----------

    [Fact]
    public async Task Update_AtualizaOsDadosEGrava()
    {
        var vehicle = TestData.AVehicle();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        var response = await _sut.UpdateAsync(
            vehicle.Id,
            new UpdateVehicleRequest("XYZ9Z99", "Toyota", "Corolla", 2025, VehicleCategory.Sedan, 210m));

        response.Plate.Should().Be("XYZ9Z99");
        response.Brand.Should().Be("Toyota");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_IgnoraOProprioVeiculoNaChecagemDePlaca()
    {
        var vehicle = TestData.AVehicle(plate: "ABC1D23");
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        await _sut.UpdateAsync(
            vehicle.Id,
            new UpdateVehicleRequest("ABC1D23", "Volkswagen", "Gol", 2024, VehicleCategory.Hatch, 130m));

        await _vehicles.Received(1).PlateExistsAsync("ABC1D23", vehicle.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_DeVeiculoInexistente_Lanca404()
    {
        _vehicles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Vehicle?)null);

        await FluentActions
            .Awaiting(() => _sut.UpdateAsync(
                Guid.NewGuid(),
                new UpdateVehicleRequest("ABC1D23", "VW", "Gol", 2024, VehicleCategory.Hatch, 100m)))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ---------- exclusão (regra 8) ----------

    [Fact]
    public async Task Delete_VeiculoDisponivel_Remove()
    {
        var vehicle = TestData.AVehicle();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        await _sut.DeleteAsync(vehicle.Id);

        _vehicles.Received(1).Remove(vehicle);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_VeiculoAlugado_LancaENaoRemove()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        await FluentActions.Awaiting(() => _sut.DeleteAsync(vehicle.Id))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*não pode ser excluído*");

        _vehicles.DidNotReceive().Remove(Arg.Any<Vehicle>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_VeiculoComHistoricoDeAlugueis_LancaENaoRemove()
    {
        var vehicle = TestData.AVehicle();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);
        _rentals.HasAnyForVehicleAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(true);

        await FluentActions.Awaiting(() => _sut.DeleteAsync(vehicle.Id))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*histórico de aluguéis*");

        _vehicles.DidNotReceive().Remove(Arg.Any<Vehicle>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ChecaOStatusAntesDeIrAoBanco()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        await FluentActions.Awaiting(() => _sut.DeleteAsync(vehicle.Id))
            .Should().ThrowAsync<DomainException>();

        // A regra mais barata roda primeiro: nem consulta o histórico.
        await _rentals.DidNotReceive().HasAnyForVehicleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_DeVeiculoInexistente_Lanca404()
    {
        _vehicles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Vehicle?)null);

        await FluentActions.Awaiting(() => _sut.DeleteAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ---------- mudança de status ----------

    [Fact]
    public async Task ChangeStatus_ParaManutencao_Aplica()
    {
        var vehicle = TestData.AVehicle();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        var response = await _sut.ChangeStatusAsync(vehicle.Id, VehicleStatus.Maintenance);

        response.Status.Should().Be(nameof(VehicleStatus.Maintenance));
    }

    [Fact]
    public async Task ChangeStatus_DeManutencaoParaDisponivel_Aplica()
    {
        var vehicle = TestData.AVehicle();
        vehicle.SendToMaintenance();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        var response = await _sut.ChangeStatusAsync(vehicle.Id, VehicleStatus.Available);

        response.Status.Should().Be(nameof(VehicleStatus.Available));
    }

    [Fact]
    public async Task ChangeStatus_ParaAlugado_Lanca()
    {
        var vehicle = TestData.AVehicle();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        await FluentActions.Awaiting(() => _sut.ChangeStatusAsync(vehicle.Id, VehicleStatus.Rented))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*não pode ser definido manualmente*");
    }

    [Fact]
    public async Task ChangeStatus_DeVeiculoAlugadoParaManutencao_Lanca()
    {
        var vehicle = TestData.AVehicle();
        vehicle.MarkAsRented();
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);

        await FluentActions.Awaiting(() => _sut.ChangeStatusAsync(vehicle.Id, VehicleStatus.Maintenance))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*está alugado*");
    }

    // ---------- listagem ----------

    [Fact]
    public async Task Search_NormalizaAPaginacaoAntesDeConsultar()
    {
        _vehicles.SearchAsync(Arg.Any<VehicleQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Vehicle>([], 1, 20, 0));

        await _sut.SearchAsync(new VehicleQuery { Page = -5, PageSize = 5_000, Brand = "  vw  " });

        await _vehicles.Received(1).SearchAsync(
            Arg.Is<VehicleQuery>(q => q.Page == 1 && q.PageSize == VehicleQuery.MaxPageSize && q.Brand == "vw"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_MapeiaAsEntidadesParaOResponsePreservandoATotalizacao()
    {
        var fleet = new[] { TestData.AVehicle(), TestData.AVehicle(plate: "XYZ9Z99") };
        _vehicles.SearchAsync(Arg.Any<VehicleQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Vehicle>(fleet, 2, 2, 7));

        var page = await _sut.SearchAsync(new VehicleQuery { Page = 2, PageSize = 2 });

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(7);
        page.TotalPages.Should().Be(4);
        page.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_DeVeiculoInexistente_Lanca404()
    {
        _vehicles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Vehicle?)null);

        await FluentActions.Awaiting(() => _sut.GetByIdAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }
}
