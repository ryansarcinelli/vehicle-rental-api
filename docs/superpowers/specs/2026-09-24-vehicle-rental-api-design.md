# Vehicle Rental API — Design

Data: 2026-09-24
Status: aprovado

## Objetivo

API REST completa para uma locadora de veículos, construída do zero com as práticas que aparecem como "preferred" em vagas US de backend .NET: CRUD, autenticação JWT, PostgreSQL via EF Core, testes unitários em xUnit, Docker Compose, Swagger e README que permite subir o projeto com um comando.

O domínio (locadora) foi escolhido por ter regras de negócio reais — disponibilidade, cálculo de valor com desconto por período, multa por atraso — que justificam a existência da camada de domínio e do projeto de testes.

## Stack

| Item | Escolha |
|---|---|
| Runtime | .NET 10 |
| Web | ASP.NET Core Web API (controllers) |
| ORM | EF Core 10 + Npgsql |
| Banco | PostgreSQL 17 (container) |
| Auth | JWT próprio (HS256), senha com BCrypt |
| Validação | FluentValidation |
| Log | Serilog (console estruturado) |
| Docs | Swashbuckle / Swagger UI com Authorize |
| Testes | xUnit + FluentAssertions + NSubstitute |
| Infra | Docker multi-stage + Docker Compose |

## Arquitetura

Clean Architecture. As dependências apontam sempre para dentro; o compilador impede violação porque `Domain` não referencia projeto nenhum.

```
VehicleRental.Api ───────────────┐
                                 ├──> VehicleRental.Application ──> VehicleRental.Domain
VehicleRental.Infrastructure ────┘
```

| Projeto | Responsabilidade | Referencia |
|---|---|---|
| `VehicleRental.Domain` | Entidades, value objects, enums, exceções de domínio. Toda regra de negócio. | nada |
| `VehicleRental.Application` | Services (casos de uso), DTOs, interfaces de repositório e de serviços externos, validadores. | Domain |
| `VehicleRental.Infrastructure` | `AppDbContext`, EF configurations, migrations, repositórios, geração de JWT, hash de senha, seed. | Application |
| `VehicleRental.Api` | Controllers, composição de DI, Swagger, middleware de exceção, pipeline de auth. | Application, Infrastructure |
| `VehicleRental.UnitTests` | Testes unitários. | Domain, Application |

Onde SOLID aparece de forma concreta:

- **Single Responsibility**: `RentalService` orquestra o caso de uso; o cálculo do valor e da multa vive dentro de `Rental`; persistência vive no repositório.
- **Dependency Inversion**: `Application` declara `IVehicleRepository`, `IRentalRepository`, `IUserRepository`, `ICustomerRepository`, `IJwtTokenGenerator`, `IPasswordHasher`, `IDateTimeProvider`. `Infrastructure` implementa. É isso que permite testar as regras com NSubstitute, sem Postgres.
- **Interface Segregation**: repositórios expõem só o que cada caso de uso usa, em vez de um `IRepository<T>` genérico com 15 métodos.

`IDateTimeProvider` existe para que as regras que dependem de "hoje" (reserva no passado, atraso na devolução) sejam determinísticas no teste.

## Modelo de domínio

### Entidades

**`User`** — `Id`, `Email`, `PasswordHash`, `Role`, `CreatedAt`
**`Customer`** — `Id`, `UserId`, `FullName`, `DriverLicense`, `Phone`
**`Vehicle`** — `Id`, `Plate`, `Brand`, `Model`, `Year`, `Category`, `DailyRate`, `Status`
**`Rental`** — `Id`, `VehicleId`, `CustomerId`, `StartDate`, `EndDate`, `ReturnedAt?`, `DailyRateSnapshot`, `TotalAmount`, `LateFee`, `Status`

`DailyRateSnapshot` congela a diária no momento da reserva: mudar o preço do veículo depois não altera aluguéis existentes.

### Value objects

- **`Plate`** — valida Mercosul (`ABC1D23`) e o formato antigo (`ABC1234`); normaliza para maiúsculo e remove hífen/espaço. Igualdade por valor.
- **`Money`** — não aceita negativo, arredonda para 2 decimais, expõe operadores de soma e multiplicação.

### Enums

- `UserRole`: `Admin`, `Customer`
- `VehicleCategory`: `Hatch`, `Sedan`, `SUV`, `Pickup`
- `VehicleStatus`: `Available`, `Rented`, `Maintenance`
- `RentalStatus`: `Active`, `Completed`, `Cancelled`

## Regras de negócio

Estas são as regras cobertas por teste unitário:

1. `Plate` rejeita formato inválido; `abc1d23` e `ABC-1D23` normalizam para `ABC1D23`.
2. `Money` rejeita valor negativo.
3. `Vehicle.Year` deve estar entre 1900 e o ano atual + 1.
4. `Rental` exige `EndDate > StartDate`, `StartDate` não anterior a hoje, e período de no máximo 90 dias.
5. Total = dias × diária, com desconto de 10% a partir de 7 dias e 15% a partir de 30.
6. Devolução após `EndDate` gera multa = dias de atraso × diária × 1.5.
7. Somente veículo `Available` pode ser alugado. Alugar muda o status para `Rented`; devolver volta para `Available`.
8. Veículo com aluguel `Active` não pode ser deletado.
9. Um cliente não pode ter dois aluguéis `Active` ao mesmo tempo.
10. `Customer` lê e cancela apenas os próprios aluguéis; `Admin` acessa todos.
11. Aluguel `Completed` ou `Cancelled` não pode ser devolvido nem cancelado de novo.
12. Login com senha errada e login com e-mail inexistente retornam o mesmo erro, sem revelar qual dos dois falhou.
13. Placa e e-mail são únicos (constraint no banco + checagem no service).

Meta de cobertura: ~50-60 testes entre `Domain` (regras puras) e `Application` (services com repositórios mockados).

## Endpoints

```
POST   /api/auth/register            publico
POST   /api/auth/login               publico -> { token, expiresAt, role }

GET    /api/vehicles                 autenticado - paginado - filtros: category, status, brand, maxDailyRate
GET    /api/vehicles/{id}            autenticado
POST   /api/vehicles                 Admin
PUT    /api/vehicles/{id}            Admin
DELETE /api/vehicles/{id}            Admin
PATCH  /api/vehicles/{id}/status     Admin (manutencao)

GET    /api/customers/me             Customer
PUT    /api/customers/me             Customer

POST   /api/rentals                  Customer
GET    /api/rentals                  Admin: todos - Customer: os proprios
GET    /api/rentals/{id}             dono ou Admin
POST   /api/rentals/{id}/return      Admin
POST   /api/rentals/{id}/cancel      dono ou Admin

GET    /health
```

Respostas de erro seguem `ProblemDetails` (RFC 7807), produzidas por um middleware global que traduz exceções de domínio em status HTTP:

| Exceção | Status |
|---|---|
| `ValidationException` (FluentValidation) | 400 |
| `DomainException` | 409 |
| `NotFoundException` | 404 |
| `ForbiddenException` | 403 |
| qualquer outra | 500 (sem detalhe vazado) |

## Docker

`Dockerfile` multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` para restore/build/publish, `mcr.microsoft.com/dotnet/aspnet:10.0` como runtime, processo rodando como usuário non-root.

`docker-compose.yml` com dois serviços:

- **`db`** — `postgres:17-alpine`, healthcheck com `pg_isready`, volume nomeado `pgdata`, porta `5432` publicada no host (necessário para o DBeaver conectar).
- **`api`** — build local, `depends_on: db` com `condition: service_healthy`, porta `8080`, connection string e `Jwt__Secret` vindos de variável de ambiente.

`.env.example` versionado; `.env` no `.gitignore`.

Em `Development` as migrations são aplicadas no startup e um seed cria o usuário Admin (`admin@vehiclerental.com`) e cerca de 8 veículos.

## Configuração do DBeaver

O DBeaver está instalado com workspace em `%APPDATA%\DBeaverData\workspace6\General\.dbeaver\`, contendo uma conexão MySQL existente que deve ser preservada.

Passos, depois do compose estar de pé:

1. Backup de `data-sources.json` e `credentials-config.json`.
2. Adicionar (não substituir) a conexão PostgreSQL `vehicle-rental (docker)` apontando para `localhost:5432/vehiclerental`, escrevendo direto nos arquivos de config.
3. Validar a conectividade de forma independente com `docker exec ... psql`, listando as tabelas criadas pelas migrations.
4. Abrir o DBeaver e confirmar visualmente que a conexão expande as tabelas.

Risco conhecido: `credentials-config.json` é criptografado pelo DBeaver. Se a escrita não for aceita pela versão instalada, a conexão aparece mas pede a senha na primeira abertura. A senha fica documentada no README.

## Fora de escopo (YAGNI)

Refresh token, CQRS/MediatR, AutoMapper, cache, paginação por cursor, soft delete, auditoria, CI, testes de integração. A arquitetura permite adicionar testes de integração depois sem reescrever nada — foi decisão explícita ficar só no unitário.
