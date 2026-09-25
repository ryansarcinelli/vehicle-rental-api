using FluentAssertions;
using NSubstitute;
using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Common;
using VehicleRental.Application.Customers;
using VehicleRental.Application.Customers.Dtos;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.UnitTests.TestSupport;

namespace VehicleRental.UnitTests.Application;

public class CustomerServiceTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly CustomerService _sut;
    private readonly Customer _customer = TestData.ACustomer();
    private readonly CurrentUser _currentUser;

    public CustomerServiceTests()
    {
        _currentUser = new CurrentUser(_customer.UserId, UserRole.Customer);
        _customers.GetByUserIdAsync(_customer.UserId, Arg.Any<CancellationToken>()).Returns(_customer);

        _sut = new CustomerService(_customers, _unitOfWork);
    }

    [Fact]
    public async Task GetMe_RetornaOPerfilDoUsuarioAutenticado()
    {
        var response = await _sut.GetMeAsync(_currentUser);

        response.Id.Should().Be(_customer.Id);
        response.FullName.Should().Be("Ryan Sarcinelli");
    }

    [Fact]
    public async Task GetMe_SemPerfilDeCliente_Lanca403()
    {
        var semPerfil = new CurrentUser(Guid.NewGuid(), UserRole.Admin);
        _customers.GetByUserIdAsync(semPerfil.UserId, Arg.Any<CancellationToken>()).Returns((Customer?)null);

        await FluentActions.Awaiting(() => _sut.GetMeAsync(semPerfil))
            .Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*não possui perfil de cliente*");
    }

    [Fact]
    public async Task UpdateMe_AtualizaEGrava()
    {
        var response = await _sut.UpdateMeAsync(
            _currentUser,
            new UpdateCustomerRequest("Ryan S. Machado", "98765432100", "27988887777"));

        response.FullName.Should().Be("Ryan S. Machado");
        response.DriverLicense.Should().Be("98765432100");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMe_NormalizaACnhComPontuacao()
    {
        var response = await _sut.UpdateMeAsync(
            _currentUser,
            new UpdateCustomerRequest("Ryan", "987.654.321-00", "27988887777"));

        response.DriverLicense.Should().Be("98765432100");
    }

    [Fact]
    public async Task UpdateMe_MantendoAPropriaCnh_NaoAcusaDuplicidade()
    {
        await _sut.UpdateMeAsync(
            _currentUser,
            new UpdateCustomerRequest("Ryan", _customer.DriverLicense, "27988887777"));

        // A CNH não mudou, então nem precisa consultar o repositório.
        await _customers.DidNotReceive()
            .DriverLicenseExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMe_ComCnhDeOutroCliente_LancaENaoGrava()
    {
        _customers.DriverLicenseExistsAsync("98765432100", _customer.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        await FluentActions
            .Awaiting(() => _sut.UpdateMeAsync(
                _currentUser,
                new UpdateCustomerRequest("Ryan", "98765432100", "27988887777")))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*já está cadastrada para outro cliente*");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")]
    [InlineData("")]
    public async Task UpdateMe_ComCnhDeTamanhoInvalido_Lanca(string driverLicense)
        => await FluentActions
            .Awaiting(() => _sut.UpdateMeAsync(
                _currentUser,
                new UpdateCustomerRequest("Ryan", driverLicense, "27988887777")))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*CNH inválida*");

    [Fact]
    public async Task UpdateMe_SemNome_Lanca()
        => await FluentActions
            .Awaiting(() => _sut.UpdateMeAsync(
                _currentUser,
                new UpdateCustomerRequest("   ", "98765432100", "27988887777")))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*obrigatório*");
}
