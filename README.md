# Vehicle Rental API

API REST de uma locadora de veículos, escrita do zero em .NET 10 com Clean Architecture, autenticação JWT, PostgreSQL via EF Core, testes unitários em xUnit, Swagger e Docker Compose.

Sobe inteira — API e banco — com um comando:

```bash
docker compose up -d
```

Depois abra <http://localhost:8080> (a raiz redireciona para o Swagger).

---

## Índice

- [Por que uma locadora](#por-que-uma-locadora)
- [Stack](#stack)
- [Arquitetura](#arquitetura)
- [Regras de negócio](#regras-de-negócio)
- [Como rodar](#como-rodar)
- [Como usar a API](#como-usar-a-api)
- [Endpoints](#endpoints)
- [Testes](#testes)
- [Banco de dados](#banco-de-dados)
- [Conectando com o DBeaver](#conectando-com-o-dbeaver)
- [Configuração](#configuração)
- [Decisões de projeto](#decisões-de-projeto)
- [Estrutura de pastas](#estrutura-de-pastas)

---

## Por que uma locadora

O domínio foi escolhido por ter regras de negócio de verdade, não só CRUD: disponibilidade do veículo por período, desconto progressivo por duração do aluguel, multa por atraso na devolução, e quem pode ver o quê. São essas regras que justificam existir uma camada de domínio separada — e é o que os testes unitários cobrem.

## Stack

| Item | Escolha |
|---|---|
| Runtime | .NET 10 |
| Web | ASP.NET Core Web API (controllers) |
| ORM | EF Core 10 + Npgsql |
| Banco | PostgreSQL 17 |
| Auth | JWT HS256 próprio, senha com BCrypt (work factor 12) |
| Validação | FluentValidation |
| Log | Serilog (console estruturado) |
| Docs | Swashbuckle / Swagger UI |
| Testes | xUnit + FluentAssertions + NSubstitute |
| Infra | Docker multi-stage + Docker Compose |

## Arquitetura

Clean Architecture com quatro projetos. As dependências apontam sempre para dentro, e isso é garantido pelo compilador: `Domain` não referencia projeto nenhum.

```
VehicleRental.Api ───────────────┐
                                 ├──> VehicleRental.Application ──> VehicleRental.Domain
VehicleRental.Infrastructure ────┘
```

| Projeto | Responsabilidade | Referencia |
|---|---|---|
| `Domain` | Entidades, value objects, enums, exceções de domínio. Toda regra de negócio. | nada |
| `Application` | Services (casos de uso), DTOs, interfaces de repositório e de serviços externos, validadores. | Domain |
| `Infrastructure` | `AppDbContext`, mapeamentos, migrations, repositórios, JWT, hash de senha, seed. | Application |
| `Api` | Controllers, DI, Swagger, middleware de exceção, pipeline de autenticação. | Application + Infrastructure |

### Onde SOLID aparece na prática

- **Single Responsibility** — `RentalService` orquestra o caso de uso; o cálculo do valor e da multa vive dentro da entidade `Rental`; persistência vive no repositório. Nenhum dos três sabe o que os outros dois fazem.
- **Open/Closed** — adicionar uma nova faixa de desconto mexe em `Rental.CalculateTotal` e em nada mais; a API, o banco e os controllers não mudam.
- **Liskov** — os repositórios concretos são substituíveis pelos mocks dos testes sem que nenhum service perceba.
- **Interface Segregation** — cada repositório expõe só os métodos que seus casos de uso usam, em vez de um `IRepository<T>` genérico com quinze métodos que ninguém chama.
- **Dependency Inversion** — `Application` **declara** `IVehicleRepository`, `IRentalRepository`, `IUserRepository`, `ICustomerRepository`, `IJwtTokenGenerator`, `IPasswordHasher`, `IDateTimeProvider`, `IUnitOfWork`. `Infrastructure` implementa. É por isso que as regras de negócio são testáveis em milissegundos, sem Postgres.

`IDateTimeProvider` existe para que as regras que dependem de "hoje" (reserva no passado, atraso na devolução) sejam determinísticas: os testes injetam um relógio parado em 2026-06-15 e o resultado não muda com o dia em que rodam.

### Value objects

Dois conceitos do domínio não são strings nem decimais soltos:

- **`Plate`** — valida os formatos Mercosul (`ABC1D23`) e antigo (`ABC1234`), normaliza para maiúsculo e remove pontuação. `abc-1d23` e `ABC1D23` são a mesma placa, e é impossível existir um `Vehicle` com placa inválida.
- **`Money`** — não aceita valor negativo, arredonda para 2 casas e tem operadores de ordem. Nenhum preço, total ou multa pode ficar negativo por acidente.

## Regras de negócio

| # | Regra |
|---|---|
| 1 | Placa é validada e normalizada; duplicidade é bloqueada (índice único + checagem no service). |
| 2 | Valor monetário nunca é negativo. |
| 3 | Ano do veículo entre 1900 e o ano atual + 1. |
| 4 | Aluguel exige devolução depois da retirada, retirada não no passado, e no máximo 90 dias. |
| 5 | Total = dias × diária, com **10% de desconto a partir de 7 dias** e **15% a partir de 30**. |
| 6 | Devolução atrasada gera multa = dias de atraso × diária × **1.5**. |
| 7 | Só veículo `Available` pode ser alugado; alugar → `Rented`, devolver → `Available`. |
| 8 | Veículo alugado não pode ser excluído. Veículo com histórico de aluguéis também não — histórico é registro financeiro; a saída é enviá-lo para manutenção. |
| 9 | Um cliente não pode ter dois aluguéis ativos ao mesmo tempo. |
| 10 | `Customer` só lê e cancela os próprios aluguéis; `Admin` acessa todos. |
| 11 | Aluguel já concluído ou cancelado não pode ser devolvido nem cancelado de novo. Cancelamento só antes da retirada. |
| 12 | Login com e-mail inexistente e login com senha errada retornam **o mesmo erro**, para não revelar quais e-mails existem na base. |
| 13 | A diária é congelada no momento da reserva: mudar o preço do veículo depois não altera aluguéis existentes. |

Todas as treze são cobertas por teste unitário.

## Como rodar

### Com Docker (recomendado)

Pré-requisito: Docker Desktop.

```bash
cp .env.example .env
```

Abra o `.env` e troque `JWT_SECRET` por um valor aleatório de 32+ caracteres. Então:

```bash
docker compose up -d --build
```

Na primeira subida a API aplica as migrations e popula o banco com um administrador e 8 veículos. Acompanhe:

```bash
docker compose logs -f api
```

Para derrubar mantendo os dados:

```bash
docker compose down
```

Para derrubar apagando o banco:

```bash
docker compose down -v
```

### Sem Docker

Pré-requisitos: .NET 10 SDK e um PostgreSQL acessível.

Suba só o banco com o compose e rode a API local:

```bash
docker compose up -d db
```

```bash
dotnet run --project src/VehicleRental.Api
```

Em `Development` a connection string padrão aponta para `localhost:5432` e as migrations são aplicadas no startup. Para gerar uma nova migration:

```bash
dotnet tool restore
```

```bash
dotnet dotnet-ef migrations add NomeDaMigration --project src/VehicleRental.Infrastructure --startup-project src/VehicleRental.Api --output-dir Persistence/Migrations
```

## Como usar a API

### Credenciais do seed

| Perfil | E-mail | Senha |
|---|---|---|
| Admin | `admin@vehiclerental.com` | `Admin@123456` |

A senha vem de `SEED_ADMIN_PASSWORD` no `.env`. Não existe rota que crie administradores — `POST /api/auth/register` sempre cria um `Customer`.

### Pelo Swagger

1. Abra <http://localhost:8080/swagger>.
2. `POST /api/auth/login` com as credenciais acima.
3. Copie o valor de `token` na resposta.
4. Clique em **Authorize** no topo da página e cole o token (sem escrever `Bearer`).
5. Os endpoints protegidos passam a funcionar.

### Pelo terminal

Login e captura do token:

```bash
curl -s -X POST http://localhost:8080/api/auth/login -H "Content-Type: application/json" -d '{"email":"admin@vehiclerental.com","password":"Admin@123456"}'
```

Listando a frota com o token:

```bash
curl -s http://localhost:8080/api/vehicles -H "Authorization: Bearer SEU_TOKEN_AQUI"
```

Cadastrando um cliente:

```bash
curl -s -X POST http://localhost:8080/api/auth/register -H "Content-Type: application/json" -d '{"email":"cliente@email.com","password":"SenhaForte1","fullName":"Cliente Exemplo","driverLicense":"12345678901","phone":"27999990000"}'
```

## Endpoints

Legenda: **livre** = sem token · **auth** = qualquer usuário autenticado.

| Método | Rota | Acesso | O que faz |
|---|---|---|---|
| `POST` | `/api/auth/register` | livre | Cadastra cliente e devolve token |
| `POST` | `/api/auth/login` | livre | Autentica e devolve token |
| `GET` | `/api/vehicles` | auth | Lista a frota, paginada e filtrável |
| `GET` | `/api/vehicles/{id}` | auth | Detalhe de um veículo |
| `POST` | `/api/vehicles` | Admin | Adiciona veículo à frota |
| `PUT` | `/api/vehicles/{id}` | Admin | Atualiza os dados do veículo |
| `PATCH` | `/api/vehicles/{id}/status` | Admin | Envia para manutenção ou libera |
| `DELETE` | `/api/vehicles/{id}` | Admin | Remove veículo sem histórico |
| `GET` | `/api/customers/me` | Customer | Perfil do cliente autenticado |
| `PUT` | `/api/customers/me` | Customer | Atualiza o próprio perfil |
| `POST` | `/api/rentals` | Customer | Cria uma reserva |
| `GET` | `/api/rentals` | auth | Admin: todos · Customer: os próprios |
| `GET` | `/api/rentals/{id}` | dono ou Admin | Detalhe do aluguel |
| `POST` | `/api/rentals/{id}/return` | Admin | Registra devolução e calcula multa |
| `POST` | `/api/rentals/{id}/cancel` | dono ou Admin | Cancela reserva não iniciada |
| `GET` | `/health` | livre | Health check (inclui o banco) |

### Filtros e paginação de `/api/vehicles`

| Parâmetro | Exemplo | Observação |
|---|---|---|
| `page` | `1` | Padrão 1 |
| `pageSize` | `20` | Padrão 20, teto 100 |
| `category` | `SUV` | `Hatch`, `Sedan`, `SUV`, `Pickup` |
| `status` | `Available` | `Available`, `Rented`, `Maintenance` |
| `brand` | `volks` | Busca parcial, ignora acentuação de caixa |
| `maxDailyRate` | `300` | Diária máxima |

```
GET /api/vehicles?category=SUV&maxDailyRate=300&page=1&pageSize=10
```

### Erros

Toda falha responde em `application/problem+json` (RFC 7807):

```json
{
  "title": "Regra de negócio violada.",
  "status": 409,
  "detail": "O cliente já possui um aluguel ativo.",
  "instance": "/api/rentals",
  "traceId": "0HNOQG153FKVR:00000001"
}
```

| Situação | Status |
|---|---|
| Payload inválido (FluentValidation) | `400` com o dicionário `errors` por campo |
| Sem token, token expirado ou credenciais erradas | `401` |
| Autenticado mas sem permissão sobre o recurso | `403` |
| Recurso inexistente | `404` |
| Regra de negócio violada | `409` |
| Erro inesperado | `500` (a mensagem original só aparece em `Development`) |

## Testes

```bash
dotnet test
```

**155 testes, todos passando**, cobrindo `Domain` e `Application`. Rodam em menos de meio segundo porque nenhum deles toca no banco — os repositórios são substituídos por mocks (NSubstitute) e o relógio é fixo.

| Arquivo | Foco |
|---|---|
| `Domain/PlateTests` | Formatos de placa, normalização, igualdade por valor |
| `Domain/MoneyTests` | Não negatividade, arredondamento, desconto, ordem |
| `Domain/VehicleTests` | Faixa de ano, transições de status, regra de exclusão |
| `Domain/RentalTests` | Período, desconto por faixa, multa por atraso, cancelamento, propriedade |
| `Application/AuthServiceTests` | Cadastro, hash da senha, mensagem única de credencial inválida |
| `Application/VehicleServiceTests` | Duplicidade de placa, exclusão, paginação, mudança de status |
| `Application/RentalServiceTests` | Aluguel ativo único, autorização por perfil, devolução, cancelamento |
| `Application/CustomerServiceTests` | Perfil próprio, CNH única |

### O que os testes *não* cobrem

Este projeto tem só testes unitários — foi decisão explícita. A consequência honesta: bugs que vivem na fronteira com o banco não aparecem aqui. Durante o desenvolvimento, dois bugs reais passaram pelos 151 testes verdes e só apareceram ao exercitar a API rodando: consultas LINQ que alcançavam dentro dos value objects (`v.Plate.Value`) não são traduzíveis pelo EF Core, e a exclusão de um veículo com histórico batia na foreign key e virava `500`. Ambos estão corrigidos, e é exatamente essa classe de defeito que testes de integração com Testcontainers pegariam. A arquitetura permite adicioná-los sem reescrever nada.

## Banco de dados

Quatro tabelas mais o histórico de migrations do EF.

```
users ──1:1── customers ──1:N── rentals ──N:1── vehicles
```

| Tabela | Observações |
|---|---|
| `users` | `Email` único; `Role` gravado como texto (`Admin`/`Customer`); senha só como hash BCrypt |
| `customers` | `DriverLicense` único, 11 dígitos; FK para `users` com cascade |
| `vehicles` | `plate` varchar(10) único; `daily_rate` numeric(10,2); `Category` e `Status` como texto |
| `rentals` | `StartDate`/`EndDate`/`ReturnedAt` como `date`; três colunas numeric(10,2); FKs `RESTRICT` |

Duas escolhas de mapeamento que valem explicação:

- **Enums como texto, não int.** Um `SELECT` no banco fica legível e uma inserção acidental de `99` não vira um status válido sem nome.
- **FKs como `RESTRICT`.** Excluir um veículo ou cliente não apaga o histórico de aluguéis em silêncio; a API bloqueia antes com `409` e uma mensagem clara.

Os value objects se tornam colunas simples via `ValueConverter`: `Plate` → `varchar(10)`, `Money` → `numeric(10,2)`. A validação continua no domínio, e o banco guarda apenas a forma já normalizada.

Inspecionando pelo terminal:

```bash
docker exec -it vehicle-rental-db psql -U postgres -d vehiclerental -c "\dt"
```

## Conectando com o DBeaver

Dados da conexão (valores padrão do `.env`):

| Campo | Valor |
|---|---|
| Host | `localhost` |
| Porta | `5432` |
| Database | `vehiclerental` |
| Usuário | `postgres` |
| Senha | `postgres` |

A porta 5432 é publicada no host pelo compose justamente para isso. O container precisa estar de pé:

```bash
docker compose ps
```

Se preferir criar a conexão pela interface: **Database → New Database Connection → PostgreSQL**, preencha os campos acima e teste. O driver JDBC é baixado pelo próprio DBeaver na primeira conexão.

## Configuração

Toda configuração entra por variável de ambiente, no padrão `Secao__Chave` do ASP.NET Core. Nada sensível é versionado: o `.env` está no `.gitignore` e o `.env.example` é o modelo.

| Variável | Padrão | Para quê |
|---|---|---|
| `POSTGRES_USER` | `postgres` | Usuário do banco |
| `POSTGRES_PASSWORD` | `postgres` | Senha do banco |
| `POSTGRES_DB` | `vehiclerental` | Nome do banco |
| `POSTGRES_PORT` | `5432` | Porta publicada no host |
| `API_PORT` | `8080` | Porta publicada da API |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Ambiente |
| `JWT_SECRET` | — | **Obrigatória**, mínimo 32 caracteres |
| `JWT_EXPIRATION_MINUTES` | `60` | Validade do token |
| `SEED_ADMIN_PASSWORD` | `Admin@123456` | Senha do admin criado no seed |

O `JWT_SECRET` é validado no startup (`ValidateOnStart`): se estiver ausente ou curto demais, a aplicação **não sobe** — falha alto e imediatamente, em vez de aceitar tokens assinados com chave fraca.

## Decisões de projeto

**JWT próprio em vez de ASP.NET Core Identity.** Identity resolveria usuários e roles com menos código, mas criaria sete tabelas e esconderia justamente a parte que interessa demonstrar. Com `IPasswordHasher` e `IJwtTokenGenerator` atrás de interfaces, o mecanismo fica explícito e trocável.

**Validação em duas camadas, de propósito.** FluentValidation rejeita payload malformado na borda (`400`); o domínio protege as invariantes (`409`). Não é redundância: a primeira dá mensagem de campo para quem consome a API, a segunda garante que nenhum caminho — nem o seed, nem um teste, nem um service futuro — consiga criar um objeto inválido.

**`CurrentUser` passado como parâmetro, não lido de `HttpContext`.** Os services recebem quem está chamando como argumento explícito. A `Application` fica sem dependência de HTTP e testar autorização vira uma linha, sem simular requisição.

**Filtro de validação escrito à mão.** A auto-validação do `FluentValidation.AspNetCore` está descontinuada pelo próprio autor. O `ValidationFilter` do projeto tem trinta linhas, resolve o validador por tipo e lança `ValidationException`, que o middleware traduz.

**`DailyRateSnapshot` no aluguel.** Sem ele, reajustar a diária de um veículo reescreveria o valor de contratos passados.

**FluentAssertions fixado na 7.2.0.** A versão 8 mudou para licença comercial. A 7.2.0 é a última sob Apache 2.0.

**Migrations aplicadas no startup, mas só sob configuração.** `Database__MigrateOnStartup` é `true` no compose e `false` por padrão. Em produção, migrar banco é passo de deploy, não efeito colateral de subir o processo.

## Estrutura de pastas

```
vehicle-rental-api/
├── docker-compose.yml           API + PostgreSQL com healthcheck
├── Dockerfile                   build multi-stage, runtime non-root
├── .env.example                 modelo das variáveis
├── VehicleRental.slnx
├── src/
│   ├── VehicleRental.Domain/
│   │   ├── Common/              Entity (identidade por Id)
│   │   ├── Entities/            User, Customer, Vehicle, Rental
│   │   ├── Enums/
│   │   ├── Exceptions/          DomainException
│   │   └── ValueObjects/        Plate, Money
│   ├── VehicleRental.Application/
│   │   ├── Abstractions/        interfaces dos repositórios e serviços
│   │   ├── Auth/                AuthService, DTOs, validadores
│   │   ├── Common/              CurrentUser, PagedResult, exceções
│   │   ├── Customers/
│   │   ├── Rentals/
│   │   └── Vehicles/
│   ├── VehicleRental.Infrastructure/
│   │   ├── Persistence/         DbContext, Configurations, Migrations, Repositories, seed
│   │   ├── Security/            JwtTokenGenerator, BCryptPasswordHasher
│   │   └── Time/                SystemDateTimeProvider
│   └── VehicleRental.Api/
│       ├── Controllers/
│       ├── Extensions/          Swagger, claims → CurrentUser
│       ├── Middleware/          exceções → ProblemDetails, ValidationFilter
│       └── Program.cs
├── tests/
│   └── VehicleRental.UnitTests/
│       ├── Application/
│       ├── Domain/
│       └── TestSupport/         relógio fixo e fábricas de teste
└── docs/
    └── superpowers/specs/       documento de design
```
