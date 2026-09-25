using FluentAssertions;
using NSubstitute;
using VehicleRental.Application.Abstractions;
using VehicleRental.Application.Auth;
using VehicleRental.Application.Auth.Dtos;
using VehicleRental.Application.Common;
using VehicleRental.Domain.Entities;
using VehicleRental.Domain.Enums;
using VehicleRental.Domain.Exceptions;
using VehicleRental.UnitTests.TestSupport;

namespace VehicleRental.UnitTests.Application;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokens = Substitute.For<IJwtTokenGenerator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FixedDateTimeProvider _clock = new();

    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(callInfo => $"hash:{callInfo.Arg<string>()}");
        _tokens.Generate(Arg.Any<User>())
            .Returns(new GeneratedToken("jwt-token", _clock.UtcNow.AddHours(1)));

        _sut = new AuthService(_users, _customers, _hasher, _tokens, _clock, _unitOfWork);
    }

    private static RegisterRequest ValidRegistration(
        string email = "Cliente@Teste.com",
        string password = "SenhaForte1")
        => new(email, password, "Ryan Sarcinelli", "123.456.789-01", "27999990000");

    // ---------- cadastro ----------

    [Fact]
    public async Task Register_ComDadosValidos_CriaUsuarioClienteEGravaUmaVezSo()
    {
        var response = await _sut.RegisterAsync(ValidRegistration());

        response.Token.Should().Be("jwt-token");
        response.Role.Should().Be(nameof(UserRole.Customer));
        response.Email.Should().Be("cliente@teste.com");

        _users.Received(1).Add(Arg.Is<User>(u => u.Role == UserRole.Customer));
        _customers.Received(1).Add(Arg.Any<Customer>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_NuncaGuardaASenhaEmTextoPuro()
    {
        await _sut.RegisterAsync(ValidRegistration(password: "SenhaForte1"));

        _hasher.Received(1).Hash("SenhaForte1");
        _users.Received(1).Add(Arg.Is<User>(u => u.PasswordHash == "hash:SenhaForte1"));
    }

    [Fact]
    public async Task Register_NormalizaOEmailEAcnh()
    {
        await _sut.RegisterAsync(ValidRegistration(email: "  Cliente@Teste.COM "));

        _users.Received(1).Add(Arg.Is<User>(u => u.Email == "cliente@teste.com"));
        _customers.Received(1).Add(Arg.Is<Customer>(c => c.DriverLicense == "12345678901"));
    }

    [Fact]
    public async Task Register_ComEmailJaCadastrado_LancaENaoGrava()
    {
        _users.EmailExistsAsync("cliente@teste.com", Arg.Any<CancellationToken>()).Returns(true);

        await FluentActions.Awaiting(() => _sut.RegisterAsync(ValidRegistration()))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*já está cadastrado*");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ComCnhJaCadastrada_LancaENaoGrava()
    {
        _customers.DriverLicenseExistsAsync("12345678901", null, Arg.Any<CancellationToken>()).Returns(true);

        await FluentActions.Awaiting(() => _sut.RegisterAsync(ValidRegistration()))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*CNH*já está cadastrada*");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("1234567")]
    [InlineData("")]
    public async Task Register_ComSenhaCurta_Lanca(string password)
        => await FluentActions.Awaiting(() => _sut.RegisterAsync(ValidRegistration(password: password)))
            .Should().ThrowAsync<DomainException>()
            .WithMessage($"*mínimo {AuthService.MinimumPasswordLength} caracteres*");

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("@dominio.com")]
    [InlineData("")]
    public async Task Register_ComEmailInvalido_Lanca(string email)
        => await FluentActions.Awaiting(() => _sut.RegisterAsync(ValidRegistration(email: email)))
            .Should().ThrowAsync<DomainException>();

    // ---------- login ----------

    [Fact]
    public async Task Login_ComCredenciaisCorretas_DevolveOToken()
    {
        var user = TestData.AUser(email: "cliente@teste.com", passwordHash: "hash-valido");
        _users.GetByEmailAsync("cliente@teste.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("SenhaForte1", "hash-valido").Returns(true);

        var response = await _sut.LoginAsync(new LoginRequest("Cliente@Teste.com", "SenhaForte1"));

        response.Token.Should().Be("jwt-token");
        response.Role.Should().Be(nameof(UserRole.Customer));
    }

    [Fact]
    public async Task Login_ComAdmin_DevolveOPerfilAdmin()
    {
        var admin = TestData.AUser(email: "admin@vehiclerental.com", role: UserRole.Admin);
        _users.GetByEmailAsync(admin.Email, Arg.Any<CancellationToken>()).Returns(admin);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var response = await _sut.LoginAsync(new LoginRequest(admin.Email, "qualquer"));

        response.Role.Should().Be(nameof(UserRole.Admin));
    }

    /// <summary>
    /// Regra 12: as duas falhas produzem exatamente a mesma exceção e a mesma mensagem,
    /// para não revelar quais e-mails existem na base.
    /// </summary>
    [Fact]
    public async Task Login_ComEmailInexistenteOuSenhaErrada_FalhaDoMesmoJeito()
    {
        _users.GetByEmailAsync("naoexiste@teste.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        var existente = TestData.AUser(email: "existe@teste.com");
        _users.GetByEmailAsync("existe@teste.com", Arg.Any<CancellationToken>()).Returns(existente);
        _hasher.Verify("errada", existente.PasswordHash).Returns(false);

        var porEmail = await FluentActions
            .Awaiting(() => _sut.LoginAsync(new LoginRequest("naoexiste@teste.com", "errada")))
            .Should().ThrowAsync<InvalidCredentialsException>();

        var porSenha = await FluentActions
            .Awaiting(() => _sut.LoginAsync(new LoginRequest("existe@teste.com", "errada")))
            .Should().ThrowAsync<InvalidCredentialsException>();

        porSenha.Which.Message.Should().Be(porEmail.Which.Message);
    }

    [Fact]
    public async Task Login_ComEmailMalFormatado_EhCredencialInvalidaENaoErroDeDominio()
    {
        await FluentActions.Awaiting(() => _sut.LoginAsync(new LoginRequest("sem-arroba", "qualquer")))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Login_NaoGravaNada()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await FluentActions.Awaiting(() => _sut.LoginAsync(new LoginRequest("a@b.com", "x")))
            .Should().ThrowAsync<InvalidCredentialsException>();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_ComHashCorrompido_NaoPropagaAExcecaoDoHasher()
    {
        var user = TestData.AUser(passwordHash: "nao-e-um-hash");
        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await FluentActions.Awaiting(() => _sut.LoginAsync(new LoginRequest(user.Email, "x")))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }
}
