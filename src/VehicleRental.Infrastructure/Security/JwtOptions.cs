namespace VehicleRental.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Chave HMAC. Vem de variável de ambiente (Jwt__Secret) — nunca versionada.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "vehicle-rental-api";

    public string Audience { get; set; } = "vehicle-rental-clients";

    public int ExpirationInMinutes { get; set; } = 60;
}
