using FluentAssertions;
using NSubstitute;
using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Rentals;
using VehicleRental.Application.Rentals.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.UnitTests.TestSupport;

namespace VehicleRental.UnitTests.Application;

public class RentalServiceTests
{
    private readonly IRentalRepository _rentals = Substitute.For<IRentalRepository>();
    private readonly IVehicleRepository _vehicles = Substitute.For<IVehicleRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FixedDateTimeProvider _clock = new();

    private readonly RentalService _sut;

    private readonly Customer _customer = TestData.ACustomer();
    private readonly CurrentUser _customerUser;
    private readonly CurrentUser _adminUser = new(Guid.NewGuid(), UserRole.Admin);

    public RentalServiceTests()
    {
        _customerUser = new CurrentUser(_customer.UserId, UserRole.Customer);
        _customers.GetByUserIdAsync(_customer.UserId, Arg.Any<CancellationToken>()).Returns(_customer);

        _sut = new RentalService(_rentals, _vehicles, _customers, _clock, _unitOfWork);
    }

    private Vehicle GivenAvailableVehicle(decimal dailyRate = 100m)
    {
        var vehicle = TestData.AVehicle(dailyRate: dailyRate);
        _vehicles.GetByIdAsync(vehicle.Id, Arg.Any<CancellationToken>()).Returns(vehicle);
        return vehicle;
    }

    private Rental GivenActiveRental(Vehicle vehicle, Guid? customerId = null, int days = 3, int startOffset = 0)
    {
        var rental = Rental.Create(
            vehicle,
            customerId ?? _customer.Id,
            TestData.Today.AddDays(startOffset),
            TestData.Today.AddDays(startOffset + days),
            TestData.Today,
            _clock.UtcNow);

        _rentals.GetByIdAsync(rental.Id, Arg.Any<CancellationToken>()).Returns(rental);
        return rental;
    }

    // ---------- criação ----------

    [Fact]
    public async Task Create_ComVeiculoDisponivel_CriaOAluguelEGrava()
    {
        var vehicle = GivenAvailableVehicle(dailyRate: 100m);

        var response = await _sut.CreateAsync(
            _customerUser,
            new CreateRentalRequest(vehicle.Id, TestData.Today, TestData.Today.AddDays(3)));

        response.Status.Should().Be(nameof(RentalStatus.Active));
        response.TotalAmount.Should().Be(300m);
        response.CustomerId.Should().Be(_customer.Id);
        vehicle.Status.Should().Be(VehicleStatus.Rented);

        _rentals.Received(1).Add(Arg.Any<Rental>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Regra 9.</summary>
    [Fact]
    public async Task Create_ComClienteQueJaTemAluguelAtivo_LancaENaoGrava()
    {
        var vehicle = GivenAvailableVehicle();
        _rentals.HasActiveRentalAsync(_customer.Id, Arg.Any<CancellationToken>()).Returns(true);

        await FluentActions
            .Awaiting(() => _sut.CreateAsync(
                _customerUser,
                new CreateRentalRequest(vehicle.Id, TestData.Today, TestData.Today.AddDays(3))))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*já possui um aluguel ativo*");

        vehicle.Status.Should().Be(VehicleStatus.Available);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ComVeiculoJaAlugado_Lanca()
    {
        var vehicle = GivenAvailableVehicle();
        vehicle.MarkAsRented();

        await FluentActions
            .Awaiting(() => _sut.CreateAsync(
                _customerUser,
                new CreateRentalRequest(vehicle.Id, TestData.Today, TestData.Today.AddDays(3))))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*não está disponível*");
    }

    [Fact]
    public async Task Create_ComVeiculoInexistente_Lanca404()
    {
        _vehicles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Vehicle?)null);

        await FluentActions
            .Awaiting(() => _sut.CreateAsync(
                _customerUser,
                new CreateRentalRequest(Guid.NewGuid(), TestData.Today, TestData.Today.AddDays(3))))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_ComUsuarioSemPerfilDeCliente_Lanca403()
    {
        var vehicle = GivenAvailableVehicle();
        _customers.GetByUserIdAsync(_adminUser.UserId, Arg.Any<CancellationToken>()).Returns((Customer?)null);

        await FluentActions
            .Awaiting(() => _sut.CreateAsync(
                _adminUser,
                new CreateRentalRequest(vehicle.Id, TestData.Today, TestData.Today.AddDays(3))))
            .Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*não possui perfil de cliente*");
    }

    [Fact]
    public async Task Create_UsaADataDeHojeDoRelogioInjetado()
    {
        var vehicle = GivenAvailableVehicle();

        // Ontem em relação ao relógio fixo do teste, independentemente do dia real.
        await FluentActions
            .Awaiting(() => _sut.CreateAsync(
                _customerUser,
                new CreateRentalRequest(vehicle.Id, TestData.Today.AddDays(-1), TestData.Today.AddDays(3))))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*não pode estar no passado*");
    }

    // ---------- listagem (regra 10) ----------

    [Fact]
    public async Task List_ComoAdmin_NaoFiltraPorCliente()
    {
        _rentals.ListAsync(null, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ListAsync(_adminUser);

        await _rentals.Received(1).ListAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_ComoCliente_FiltraPeloProprioCliente()
    {
        _rentals.ListAsync(_customer.Id, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.ListAsync(_customerUser);

        await _rentals.Received(1).ListAsync(_customer.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_ComoDono_Retorna()
    {
        var rental = GivenActiveRental(GivenAvailableVehicle());

        var response = await _sut.GetByIdAsync(_customerUser, rental.Id);

        response.Id.Should().Be(rental.Id);
    }

    [Fact]
    public async Task GetById_ComoAdmin_RetornaAluguelDeQualquerCliente()
    {
        var rental = GivenActiveRental(GivenAvailableVehicle(), customerId: Guid.NewGuid());

        var response = await _sut.GetByIdAsync(_adminUser, rental.Id);

        response.Id.Should().Be(rental.Id);
    }

    [Fact]
    public async Task GetById_DeAluguelDeOutroCliente_Lanca403()
    {
        var rental = GivenActiveRental(GivenAvailableVehicle(), customerId: Guid.NewGuid());

        await FluentActions.Awaiting(() => _sut.GetByIdAsync(_customerUser, rental.Id))
            .Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*outro cliente*");
    }

    [Fact]
    public async Task GetById_DeAluguelInexistente_Lanca404()
    {
        _rentals.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Rental?)null);

        await FluentActions.Awaiting(() => _sut.GetByIdAsync(_adminUser, Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ---------- devolução ----------

    [Fact]
    public async Task Return_ComoAdmin_EncerraOAluguelELiberaOVeiculo()
    {
        var vehicle = GivenAvailableVehicle();
        var rental = GivenActiveRental(vehicle, days: 3);

        var response = await _sut.ReturnAsync(_adminUser, rental.Id, new ReturnRentalRequest(null));

        response.Status.Should().Be(nameof(RentalStatus.Completed));
        response.LateFee.Should().Be(0m);
        vehicle.Status.Should().Be(VehicleStatus.Available);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Return_SemDataInformada_UsaHoje()
    {
        var vehicle = GivenAvailableVehicle();
        var rental = GivenActiveRental(vehicle, days: 3);

        var response = await _sut.ReturnAsync(_adminUser, rental.Id, new ReturnRentalRequest(null));

        response.ReturnedAt.Should().Be(_clock.Today);
    }

    [Fact]
    public async Task Return_ComAtraso_CobraAMulta()
    {
        var vehicle = GivenAvailableVehicle(dailyRate: 100m);
        var rental = GivenActiveRental(vehicle, days: 3);

        var response = await _sut.ReturnAsync(
            _adminUser,
            rental.Id,
            new ReturnRentalRequest(rental.EndDate.AddDays(2)));

        response.LateFee.Should().Be(300m);
        response.AmountDue.Should().Be(600m);
    }

    [Fact]
    public async Task Return_ComoCliente_Lanca403()
    {
        var rental = GivenActiveRental(GivenAvailableVehicle());

        await FluentActions
            .Awaiting(() => _sut.ReturnAsync(_customerUser, rental.Id, new ReturnRentalRequest(null)))
            .Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Apenas administradores*");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- cancelamento ----------

    [Fact]
    public async Task Cancel_ComoDonoAntesDaRetirada_CancelaELiberaOVeiculo()
    {
        var vehicle = GivenAvailableVehicle();
        var rental = GivenActiveRental(vehicle, startOffset: 2);

        var response = await _sut.CancelAsync(_customerUser, rental.Id);

        response.Status.Should().Be(nameof(RentalStatus.Cancelled));
        vehicle.Status.Should().Be(VehicleStatus.Available);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_DeAluguelDeOutroCliente_Lanca403()
    {
        var rental = GivenActiveRental(GivenAvailableVehicle(), customerId: Guid.NewGuid(), startOffset: 2);

        await FluentActions.Awaiting(() => _sut.CancelAsync(_customerUser, rental.Id))
            .Should().ThrowAsync<ForbiddenException>();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_DepoisDeIniciado_Lanca()
    {
        var rental = GivenActiveRental(GivenAvailableVehicle(), startOffset: 0);

        await FluentActions.Awaiting(() => _sut.CancelAsync(_customerUser, rental.Id))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*já foi iniciado*");
    }

    [Fact]
    public async Task Cancel_ComoAdmin_CancelaAluguelDeQualquerCliente()
    {
        var vehicle = GivenAvailableVehicle();
        var rental = GivenActiveRental(vehicle, customerId: Guid.NewGuid(), startOffset: 2);

        var response = await _sut.CancelAsync(_adminUser, rental.Id);

        response.Status.Should().Be(nameof(RentalStatus.Cancelled));
    }
}
