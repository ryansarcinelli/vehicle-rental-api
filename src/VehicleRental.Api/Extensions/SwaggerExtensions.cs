using System.Reflection;
using Microsoft.OpenApi;

namespace VehicleRental.Api.Extensions;

public static class SwaggerExtensions
{
    private const string SecuritySchemeId = "Bearer";

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Vehicle Rental API",
                Version = "v1",
                Description = """
                    API de locadora de veículos construída com Clean Architecture.

                    Para testar os endpoints protegidos:
                    1. `POST /api/auth/login` com um usuário válido.
                    2. Copie o campo `token` da resposta.
                    3. Clique em **Authorize** acima e cole o token.
                    """
            });

            // Faz o Swagger UI ganhar o botão Authorize e enviar o header
            // Authorization: Bearer <token> nas chamadas seguintes.
            options.AddSecurityDefinition(SecuritySchemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe apenas o token; o prefixo 'Bearer' é adicionado automaticamente."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SecuritySchemeId, document)] = []
            });

            // Usa os comentários XML dos controllers como descrição dos endpoints.
            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }
}
