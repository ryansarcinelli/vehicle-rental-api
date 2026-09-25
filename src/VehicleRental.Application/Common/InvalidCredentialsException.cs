namespace VehicleRental.Application.Common;

/// <summary>
/// Regra 12: e-mail inexistente e senha errada produzem exatamente o mesmo erro, para não
/// revelar quais e-mails existem na base. A API traduz para 401.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("E-mail ou senha inválidos.")
    {
    }
}
