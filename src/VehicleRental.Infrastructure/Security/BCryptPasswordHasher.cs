using VehicleRental.Application.Abstractions;

namespace VehicleRental.Infrastructure.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// 12 rounds: custo suficiente em 2026 sem tornar o login perceptivelmente lento.
    /// </summary>
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrompido ou em formato desconhecido não deve derrubar o login.
            return false;
        }
    }
}
