# Plano de Implementação - Sistema de Microserviços com .NET Aspire

## Visão Geral
Este documento descreve o plano detalhado para implementar um sistema completo de microserviços usando .NET 8 e .NET Aspire 9.0+ para orquestração, incluindo três serviços principais: Catalog.Api, Orders.Api e Notifications.Worker.

## Arquitetura do Sistema

```
┌─────────────────────────────────────────────────────────────┐
│                      Aspire AppHost                          │
│  (Orquestração, Service Discovery, Observabilidade)         │
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
    ┌─────▼─────┐      ┌─────▼─────┐      ┌─────▼─────┐
    │ Catalog   │      │  Orders   │      │Notifications│
    │   .Api    │◄─────┤   .Api    │      │  .Worker  │
    └─────┬─────┘      └─────┬─────┘      └─────▲─────┘
          │                  │                   │
    ┌─────▼─────┐      ┌─────▼─────┐      ┌─────┴─────┐
    │SQL Server │      │SQL Server │      │ RabbitMQ  │
    │(CatalogDb)│      │ (OrdersDb)│      └───────────┘
    └───────────┘      └───────────┘
          │
    ┌─────▼─────┐
    │   Redis   │
    │  (Cache)  │
    └───────────┘
```

## Fase 1: Inicialização do Repositório Git

### Tarefas:
1. **Inicializar repositório Git**
   ```bash
   git init
   ```

2. **Criar branch de desenvolvimento**
   ```bash
   git checkout -b feature/microservices-setup
   ```

3. **Criar `.gitignore` para .NET**
   - Ignorar bin/, obj/, .vs/, *.user, etc.
   - Incluir padrões específicos do Aspire

### Entregáveis:
- Repositório Git inicializado
- Branch `feature/microservices-setup` criado
- Arquivo `.gitignore` configurado

---

## Fase 2: Estrutura Base do Aspire

### Tarefas:

1. **Criar pasta src**
   ```bash
   mkdir src
   ```

2. **Criar Solution**
   ```bash
   dotnet new sln -n MicroservicesAspire
   ```

3. **Criar Projeto AppHost**
   ```bash
   dotnet new aspire-apphost -n MicroservicesAspire.AppHost -o src/MicroservicesAspire.AppHost
   dotnet sln add src/MicroservicesAspire.AppHost
   ```

4. **Criar Projeto ServiceDefaults**
   ```bash
   dotnet new aspire-servicedefaults -n MicroservicesAspire.ServiceDefaults -o src/MicroservicesAspire.ServiceDefaults
   dotnet sln add src/MicroservicesAspire.ServiceDefaults
   ```

5. **Configurar ServiceDefaults com:**
   - **OpenTelemetry**: Traces, Metrics, Logs
   - **Health Checks**: Padrão para todos os serviços
   - **Service Discovery**: Configuração automática
   - **Serilog**: Logging estruturado com enrichers

### Estrutura de Diretórios:
```
claudicando/
├── docs/
│   └── IMPLEMENTATION_PLAN.md
├── src/
│   ├── MicroservicesAspire.AppHost/
│   ├── MicroservicesAspire.ServiceDefaults/
│   ├── Catalog.Api/
│   ├── Orders.Api/
│   └── Notifications.Worker/
├── MicroservicesAspire.sln
├── .gitignore
└── README.md
```

### Entregáveis:
- Solution criada
- AppHost configurado
- ServiceDefaults com configurações padrão

---

## Fase 3: Microserviço Catalog.Api

### Tarefas:

1. **Criar Projeto Web API**
   ```bash
   dotnet new web -n Catalog.Api -o src/Catalog.Api
   dotnet sln add src/Catalog.Api
   dotnet add src/Catalog.Api reference src/MicroservicesAspire.ServiceDefaults
   ```

2. **Adicionar Pacotes NuGet:**
   - Microsoft.EntityFrameworkCore.SqlServer
   - Microsoft.EntityFrameworkCore.Tools
   - Microsoft.Extensions.Caching.StackExchangeRedis
   - Swashbuckle.AspNetCore
   - FluentValidation.AspNetCore
   - Serilog.AspNetCore

3. **Implementar Camadas:**
   - **Entities**: Product (Id, Name, Description, Price, Stock)
   - **DTOs**: ProductDto, CreateProductDto, UpdateProductDto (record types)
   - **DbContext**: CatalogDbContext
   - **Repository**: IProductRepository, ProductRepository
   - **Validators**: CreateProductValidator, UpdateProductValidator

4. **Implementar Endpoints (Minimal APIs):**
   - `GET /api/products` - Lista produtos (com cache)
   - `GET /api/products/{id}` - Busca produto específico
   - `POST /api/products` - Cria produto
   - `PUT /api/products/{id}` - Atualiza produto
   - `DELETE /api/products/{id}` - Remove produto

5. **Configurações:**
   - AddServiceDefaults()
   - Configure Redis caching
   - Configure Swagger/OpenAPI
   - Configure CORS
   - Health Checks específicos (DB, Redis)

6. **Migrations:**
   ```bash
   dotnet ef migrations add InitialCreate -p src/Catalog.Api
   dotnet ef database update -p src/Catalog.Api
   ```

### Entregáveis:
- Catalog.Api funcional com CRUD completo
- Caching com Redis
- Validação implementada
- Swagger configurado

---

## Fase 4: Microserviço Orders.Api

### Tarefas:

1. **Criar Projeto Web API**
   ```bash
   dotnet new web -n Orders.Api -o src/Orders.Api
   dotnet sln add src/Orders.Api
   dotnet add src/Orders.Api reference src/MicroservicesAspire.ServiceDefaults
   ```

2. **Adicionar Pacotes NuGet:**
   - Microsoft.EntityFrameworkCore.SqlServer
   - Microsoft.EntityFrameworkCore.Tools
   - RabbitMQ.Client
   - Swashbuckle.AspNetCore
   - FluentValidation.AspNetCore

3. **Implementar Camadas:**
   - **Entities**: Order (Id, UserId, Items, TotalAmount, CreatedAt, Status)
   - **Entities**: OrderItem (Id, OrderId, ProductId, ProductName, Quantity, Price)
   - **DTOs**: OrderDto, CreateOrderDto, OrderItemDto (record types)
   - **DbContext**: OrdersDbContext
   - **Repository**: IOrderRepository, OrderRepository
   - **Services**: ICatalogService (HttpClient), IRabbitMqPublisher
   - **Validators**: CreateOrderValidator

4. **Implementar Endpoints (Minimal APIs):**
   - `POST /api/orders` - Cria pedido
   - `GET /api/orders/{id}` - Consulta pedido
   - `GET /api/orders` - Lista pedidos (query: userId)

5. **Service Discovery:**
   - Configurar HttpClient para Catalog.Api usando Service Discovery
   - Validar produtos antes de criar pedido
   - Verificar stock disponível

6. **RabbitMQ Integration:**
   - Publicar evento `OrderCreated` após criação do pedido
   - Payload: { OrderId, UserId, Items[], TotalAmount, CreatedAt }

7. **Migrations:**
   ```bash
   dotnet ef migrations add InitialCreate -p src/Orders.Api
   dotnet ef database update -p src/Orders.Api
   ```

### Entregáveis:
- Orders.Api funcional
- Integração com Catalog.Api via Service Discovery
- Publicação de eventos no RabbitMQ
- Validação de produtos

---

## Fase 5: Worker Notifications.Worker

### Tarefas:

1. **Criar Worker Service**
   ```bash
   dotnet new worker -n Notifications.Worker -o src/Notifications.Worker
   dotnet sln add src/Notifications.Worker
   dotnet add src/Notifications.Worker reference src/MicroservicesAspire.ServiceDefaults
   ```

2. **Adicionar Pacotes NuGet:**
   - RabbitMQ.Client

3. **Implementar Consumer:**
   - Background Service que consome fila do RabbitMQ
   - Processa eventos `OrderCreated`
   - Loga notificação estruturada (simulação)

4. **Logging:**
   - Usar Serilog para logs estruturados
   - Incluir OrderId, UserId, TotalAmount nos logs
   - Log levels apropriados (Information para sucesso, Error para falhas)

### Entregáveis:
- Worker funcional consumindo RabbitMQ
- Logging estruturado
- Tratamento de erros e retry

---

## Fase 6: Configuração do AppHost

### Tarefas:

1. **Configurar Recursos no Program.cs:**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Recursos de infraestrutura
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = sqlServer.AddDatabase("catalogdb");
var ordersDb = sqlServer.AddDatabase("ordersdb");

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

// Microserviços
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WaitFor(catalogDb)
    .WaitFor(redis);

var ordersApi = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WithReference(rabbitMq)
    .WithReference(catalogApi)
    .WaitFor(ordersDb)
    .WaitFor(rabbitMq)
    .WaitFor(catalogApi);

var notificationsWorker = builder.AddProject<Projects.Notifications_Worker>("notifications-worker")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.Build().Run();
```

2. **Configurar Dependências:**
   - Orders.Api depende de Catalog.Api, OrdersDb e RabbitMQ
   - Catalog.Api depende de CatalogDb e Redis
   - Notifications.Worker depende de RabbitMQ

3. **Configurar Ordem de Inicialização:**
   - Usar WaitFor() para garantir que dependências estejam prontas

### Entregáveis:
- AppHost completo
- Service Discovery configurado
- Dependências corretas
- Container lifecycle management

---

## Fase 7: Documentação

### Tarefas:

1. **README.md Principal:**
   - Visão geral da arquitetura
   - Diagrama de comunicação
   - Pré-requisitos (.NET 8 SDK, Docker Desktop)
   - Instruções de execução
   - Como acessar Aspire Dashboard (https://localhost:15888)
   - Endpoints disponíveis
   - Variáveis de ambiente

2. **Arquivo .http (exemplos de requisições):**
```http
### Criar produto no Catalog
POST https://localhost:7001/api/products
Content-Type: application/json

{
  "name": "Notebook Dell",
  "description": "Notebook Dell Inspiron 15",
  "price": 3500.00,
  "stock": 10
}

### Criar pedido no Orders
POST https://localhost:7002/api/orders
Content-Type: application/json

{
  "userId": "user-123",
  "items": [
    {
      "productId": "{{productId}}",
      "quantity": 2
    }
  ]
}
```

3. **Scripts Úteis (scripts/*):**
   - `run-migrations.sh` - Executar migrations
   - `seed-data.sh` - Popular dados iniciais
   - `clean.sh` - Limpar containers e volumes

4. **Documentação de Decisões Arquiteturais (docs/ADR.md):**
   - Por que .NET Aspire?
   - Por que RabbitMQ para mensageria?
   - Por que Redis para cache?
   - Padrões escolhidos (Repository, Minimal APIs)

### Entregáveis:
- README.md completo
- Arquivo .http com exemplos
- Scripts de automação
- Documentação de decisões

---

## Fase 8: Testes e Validação

### Tarefas:

1. **Executar AppHost:**
   ```bash
   dotnet run --project src/MicroservicesAspire.AppHost
   ```

2. **Verificar Aspire Dashboard:**
   - Acessar https://localhost:15888
   - Verificar status de todos os recursos
   - Verificar traces e metrics

3. **Testar Fluxo Completo:**
   - Criar produtos via Catalog.Api
   - Criar pedido via Orders.Api (valida produtos)
   - Verificar evento publicado no RabbitMQ
   - Verificar logs do Notifications.Worker

4. **Validar Observabilidade:**
   - Traces distribuídos no Aspire Dashboard
   - Metrics de performance
   - Logs estruturados
   - Health checks

5. **Testar Cenários de Erro:**
   - Produto não encontrado
   - Stock insuficiente
   - RabbitMQ indisponível

### Entregáveis:
- Sistema completo funcionando
- Observabilidade validada
- Documentação de testes

---

## Stack Técnica Completa

### Runtime & Frameworks:
- .NET 8 (compatível com .NET 10)
- ASP.NET Core 8.0
- .NET Aspire 9.0+

### Banco de Dados:
- SQL Server (via Docker)
- Entity Framework Core 8.0

### Mensageria:
- RabbitMQ (via Docker)
- RabbitMQ.Client

### Cache:
- Redis (via Docker)
- StackExchange.Redis

### Logging:
- Serilog
- OpenTelemetry

### Documentação:
- Swashbuckle.AspNetCore (Swagger/OpenAPI)

### Validação:
- FluentValidation

### Observabilidade:
- Aspire Dashboard
- OpenTelemetry (Traces, Metrics, Logs)
- Prometheus-compatible metrics

---

## Boas Práticas Implementadas

1. **Record Types para DTOs**: Imutabilidade e sintaxe concisa
2. **Minimal APIs**: Performance e simplicidade
3. **Repository Pattern**: Abstração de acesso a dados
4. **Service Discovery**: Comunicação resiliente entre serviços
5. **Health Checks**: Monitoramento de saúde dos serviços
6. **Structured Logging**: Logs estruturados com contexto
7. **Validation**: FluentValidation para regras de negócio
8. **CORS**: Configuração apropriada para ambiente de desenvolvimento
9. **Error Handling**: Tratamento centralizado de erros
10. **OpenTelemetry**: Observabilidade distribuída

---

## Cronograma Estimado

| Fase | Estimativa | Complexidade |
|------|-----------|--------------|
| Fase 1: Git Init | 5 min | Baixa |
| Fase 2: Aspire Base | 15 min | Média |
| Fase 3: Catalog.Api | 45 min | Alta |
| Fase 4: Orders.Api | 60 min | Alta |
| Fase 5: Notifications.Worker | 30 min | Média |
| Fase 6: AppHost Config | 20 min | Média |
| Fase 7: Documentação | 30 min | Baixa |
| Fase 8: Testes | 20 min | Média |
| **TOTAL** | **~3.5 horas** | - |

---

## Próximos Passos

Após confirmar este plano, a implementação seguirá a ordem das fases descritas acima, começando pela inicialização do Git e criação da estrutura base do Aspire.

**Pronto para começar?** 🚀
