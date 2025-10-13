# Convenções do Projeto - Sistema de Microserviços com .NET Aspire

## Estrutura de Diretórios

**IMPORTANTE**: Todos os projetos .NET devem ser criados dentro da pasta `src/`.

```
claudicando/
├── .claude/
│   ├── CLAUDE.md          # Este arquivo - convenções do projeto
│   └── settings.local.json
├── docs/
│   ├── IMPLEMENTATION_PLAN.md
│   └── ADR.md             # Architectural Decision Records
├── scripts/
│   ├── run-migrations.sh
│   ├── seed-data.sh
│   └── clean.sh
├── src/                   # ⚠️ TODOS OS PROJETOS DEVEM ESTAR AQUI
│   ├── MicroservicesAspire.AppHost/
│   ├── MicroservicesAspire.ServiceDefaults/
│   ├── Catalog.Api/
│   ├── Orders.Api/
│   └── Notifications.Worker/
├── tests/                 # Projetos de teste (futuramente)
├── .gitignore
├── MicroservicesAspire.sln
└── README.md
```

## Regras para Criação de Projetos

### 1. Localização
- **TODOS** os projetos .NET devem ser criados em `src/`
- Use a flag `-o src/NomeDoProjeto` ao criar novos projetos
- Exemplo: `dotnet new web -n Catalog.Api -o src/Catalog.Api`

### 2. Adição à Solution
- Sempre adicione o caminho completo: `dotnet sln add src/NomeDoProjeto`
- Nunca adicione projetos sem o prefixo `src/`

### 3. Referências entre Projetos
- Use sempre caminhos relativos à raiz
- Exemplo: `dotnet add src/Catalog.Api reference src/MicroservicesAspire.ServiceDefaults`

### 4. Nomenclatura
- **AppHost**: `MicroservicesAspire.AppHost`
- **ServiceDefaults**: `MicroservicesAspire.ServiceDefaults`
- **APIs**: Sufixo `.Api` (ex: `Catalog.Api`, `Orders.Api`)
- **Workers**: Sufixo `.Worker` (ex: `Notifications.Worker`)

## Arquitetura dos Microserviços

### Estrutura Interna de cada API
```
Catalog.Api/
├── Entities/          # Domain models
├── DTOs/              # Data Transfer Objects (record types)
├── Data/              # DbContext e Repositories
│   ├── CatalogDbContext.cs
│   └── Repositories/
├── Validators/        # FluentValidation
├── Services/          # Business logic
├── Endpoints/         # Minimal API endpoints (opcional - pode estar no Program.cs)
├── Program.cs
└── appsettings.json
```

### Padrões Obrigatórios

#### 1. DTOs como Record Types
```csharp
public record ProductDto(Guid Id, string Name, string Description, decimal Price, int Stock);
public record CreateProductDto(string Name, string Description, decimal Price, int Stock);
public record UpdateProductDto(string Name, string Description, decimal Price, int Stock);
```

#### 2. Repository Pattern
```csharp
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product> CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(Guid id);
}
```

#### 3. Service Discovery (Orders → Catalog)
```csharp
// Program.cs do Orders.Api
builder.Services.AddHttpClient<ICatalogService>((serviceProvider, client) =>
{
    // Service Discovery automático via Aspire
    var catalogUrl = serviceProvider.GetRequiredService<IConfiguration>()
        .GetConnectionString("catalog-api");
    client.BaseAddress = new Uri(catalogUrl);
});
```

#### 4. Configuração no Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);

// SEMPRE adicionar ServiceDefaults primeiro
builder.AddServiceDefaults();

// Depois adicionar outros serviços
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("catalogdb")));

// Redis, FluentValidation, etc.
```

## Aspire AppHost

### Template do Program.cs
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// 1. RECURSOS DE INFRAESTRUTURA
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = sqlServer.AddDatabase("catalogdb");
var ordersDb = sqlServer.AddDatabase("ordersdb");

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

// 2. MICROSERVIÇOS
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WaitFor(catalogDb)
    .WaitFor(redis);

var ordersApi = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WithReference(rabbitMq)
    .WithReference(catalogApi)  // Service Discovery
    .WaitFor(ordersDb)
    .WaitFor(rabbitMq)
    .WaitFor(catalogApi);

var notificationsWorker = builder.AddProject<Projects.Notifications_Worker>("notifications-worker")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.Build().Run();
```

### Regras de Dependência
- Use `WithReference()` para estabelecer dependências
- Use `WaitFor()` para ordem de inicialização
- Nome do serviço no AppHost deve corresponder à connection string esperada

## Comandos Essenciais

### Criar Novo Projeto
```bash
# API
dotnet new web -n NomeDoProjeto.Api -o src/NomeDoProjeto.Api
dotnet sln add src/NomeDoProjeto.Api
dotnet add src/NomeDoProjeto.Api reference src/MicroservicesAspire.ServiceDefaults

# Worker
dotnet new worker -n NomeDoProjeto.Worker -o src/NomeDoProjeto.Worker
dotnet sln add src/NomeDoProjeto.Worker
dotnet add src/NomeDoProjeto.Worker reference src/MicroservicesAspire.ServiceDefaults
```

### Entity Framework Migrations
```bash
# Criar migration
dotnet ef migrations add MigrationName -p src/Catalog.Api

# Aplicar migration
dotnet ef database update -p src/Catalog.Api

# Remover última migration
dotnet ef migrations remove -p src/Catalog.Api
```

### Executar AppHost
```bash
dotnet run --project src/MicroservicesAspire.AppHost
```

### Build e Restore
```bash
# Restore de todos os projetos
dotnet restore

# Build de todos os projetos
dotnet build

# Build de projeto específico
dotnet build src/Catalog.Api
```

## Boas Práticas

### 1. Logging
- Use Serilog (configurado via ServiceDefaults)
- Sempre adicione contexto: OrderId, UserId, etc.
- Níveis apropriados: Information, Warning, Error

```csharp
_logger.LogInformation("Order {OrderId} created for user {UserId}", orderId, userId);
```

### 2. Validação
- Use FluentValidation para validação de DTOs
- Registre validators no DI
- Valide antes de processar

```csharp
public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}
```

### 3. Health Checks
- Sempre configure health checks para dependências
- DbContext, Redis, RabbitMQ

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>()
    .AddRedis(builder.Configuration.GetConnectionString("redis")!);
```

### 4. Error Handling
- Use middleware de tratamento de erros
- Retorne Problem Details (RFC 7807)
- Nunca exponha stack traces em produção

### 5. CORS
- Configure CORS apropriadamente
- Restritivo em produção, permissivo em desenvolvimento

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

## Pacotes NuGet Essenciais

### APIs (Catalog e Orders)
- Aspire.Hosting.AppHost
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.EntityFrameworkCore.Tools
- Swashbuckle.AspNetCore
- FluentValidation.AspNetCore

### Catalog.Api específico
- Microsoft.Extensions.Caching.StackExchangeRedis
- Aspire.StackExchange.Redis

### Orders.Api específico
- RabbitMQ.Client
- Aspire.RabbitMQ.Client

### Notifications.Worker
- RabbitMQ.Client

### ServiceDefaults
- Microsoft.Extensions.Http.Resilience
- Microsoft.Extensions.ServiceDiscovery
- OpenTelemetry.Exporter.OpenTelemetryProtocol
- OpenTelemetry.Extensions.Hosting
- OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry.Instrumentation.Http
- OpenTelemetry.Instrumentation.Runtime

## Git Workflow

### Branches
- `main`: código estável
- `feature/*`: novas features
- `fix/*`: correções

### Commits
- Mensagens descritivas em português
- Prefixos: `feat:`, `fix:`, `docs:`, `refactor:`
- Exemplo: `feat: adiciona endpoint de criação de produtos`

### Merge
- Sempre revisar antes de merge para main
- Executar testes antes de merge

## Troubleshooting

### Docker Desktop não está rodando
```bash
# Verificar containers
docker ps

# Iniciar Docker Desktop
# Windows: Abrir Docker Desktop manualmente
```

### Migrations não aplicam
```bash
# Verificar connection string
dotnet user-secrets list -p src/Catalog.Api

# Forçar aplicação
dotnet ef database update --force -p src/Catalog.Api
```

### Aspire Dashboard não abre
- Verificar se porta 15888 está disponível
- Verificar logs do AppHost
- Reiniciar AppHost

## Observabilidade

### Aspire Dashboard
- URL padrão: https://localhost:15888
- Traces: Visualizar requisições distribuídas
- Metrics: Performance dos serviços
- Logs: Logs estruturados de todos os serviços

### Endpoints de Observabilidade
- `/health`: Health check
- `/health/ready`: Readiness probe
- `/health/live`: Liveness probe

## Referências

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [FluentValidation](https://docs.fluentvalidation.net/)
- [Serilog](https://serilog.net/)
- [RabbitMQ .NET Client](https://www.rabbitmq.com/dotnet.html)

---

**Última atualização**: 2025-01-13
**Versão do Plano**: 1.1
