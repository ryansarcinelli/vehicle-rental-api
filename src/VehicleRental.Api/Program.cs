using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using VehicleRental.Api.Extensions;
using VehicleRental.Api.Middleware;
using VehicleRental.Application;
using VehicleRental.Infrastructure;
using VehicleRental.Infrastructure.Persistence;
using VehicleRental.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("Seção de configuração 'Jwt' ausente.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

// Primeiro da cadeia: qualquer exceção levantada adiante vira ProblemDetails.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Vehicle Rental API v1");
    options.DocumentTitle = "Vehicle Rental API";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Abre a raiz direto no Swagger: quem sobe o container acha a documentação sem procurar.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await MigrateAndSeedAsync(app);

app.Run();

// Aplica as migrations pendentes e popula o seed no startup. Faz isso apenas quando
// habilitado por configuração, para que um ambiente real controle a migração por fora.
static async Task MigrateAndSeedAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:MigrateOnStartup", false))
        return;

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    logger.LogInformation("Migrations aplicadas.");

    var adminPassword = app.Configuration["Seed:AdminPassword"];

    if (string.IsNullOrWhiteSpace(adminPassword))
    {
        logger.LogWarning("Seed:AdminPassword não configurada; o seed foi ignorado.");
        return;
    }

    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(adminPassword);
}
