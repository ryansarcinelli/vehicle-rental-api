# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia só os manifestos primeiro: o restore fica em cache enquanto as dependências
# não mudarem, mesmo que o código mude.
COPY VehicleRental.slnx .
COPY src/VehicleRental.Domain/VehicleRental.Domain.csproj src/VehicleRental.Domain/
COPY src/VehicleRental.Application/VehicleRental.Application.csproj src/VehicleRental.Application/
COPY src/VehicleRental.Infrastructure/VehicleRental.Infrastructure.csproj src/VehicleRental.Infrastructure/
COPY src/VehicleRental.Api/VehicleRental.Api.csproj src/VehicleRental.Api/
COPY tests/VehicleRental.UnitTests/VehicleRental.UnitTests.csproj tests/VehicleRental.UnitTests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/VehicleRental.Api/VehicleRental.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# O Npgsql procura a biblioteca GSSAPI ao abrir a conexão; sem ela o driver funciona,
# mas polui o log de startup com um erro de carregamento.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# $APP_UID é o usuário sem privilégios que as imagens .NET já trazem: o processo
# não roda como root dentro do container.
USER $APP_UID

COPY --from=build --chown=$APP_UID /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

ENTRYPOINT ["dotnet", "VehicleRental.Api.dll"]
