namespace VehicleRental.Application.Common;

/// <summary>
/// Recurso inexistente. A API traduz para 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} não encontrado para o identificador '{key}'.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}
