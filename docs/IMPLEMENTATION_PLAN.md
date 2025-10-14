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
    └───────────┘      │ - Orders  │            ▲
          │            │ - Outbox  │◄───────────┘
    ┌─────▼─────┐      └───────────┘     Outbox Processor
    │   Redis   │            (Background Service)
    │  (Cache)  │
    └───────────┘
```

**Padrão Outbox**: O Orders.Api utiliza o padrão Transactional Outbox para garantir consistência entre a criação de pedidos e o envio de eventos. Mensagens são salvas na tabela Outbox na mesma transação do pedido, e um background service (Outbox Processor) processa e publica os eventos no RabbitMQ de forma assíncrona e confiável.

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

## Fase 4: Microserviço Orders.Api (com Padrão Outbox)

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
   - **Entities**: OutboxMessage (Id, EventType, Payload, CreatedAt, ProcessedAt, IsProcessed)
   - **DTOs**: OrderDto, CreateOrderDto, OrderItemDto (record types)
   - **DbContext**: OrdersDbContext (incluir DbSet<OutboxMessage>)
   - **Repository**: IOrderRepository, OrderRepository
   - **Services**: ICatalogService (HttpClient)
   - **Validators**: CreateOrderValidator

4. **Implementar Endpoints (Minimal APIs):**
   - `POST /api/orders` - Cria pedido e salva evento na Outbox (transação única)
   - `GET /api/orders/{id}` - Consulta pedido
   - `GET /api/orders` - Lista pedidos (query: userId)

5. **Service Discovery:**
   - Configurar HttpClient para Catalog.Api usando Service Discovery
   - Validar produtos antes de criar pedido
   - Verificar stock disponível

6. **Padrão Outbox (Transactional Outbox Pattern):**
   - Criar entidade `OutboxMessage` com campos:
     - `Id` (Guid, PK)
     - `EventType` (string, ex: "OrderCreated")
     - `Payload` (string, JSON serializado)
     - `CreatedAt` (DateTime)
     - `ProcessedAt` (DateTime?)
     - `IsProcessed` (bool, default: false)
   - No endpoint `POST /api/orders`:
     - Iniciar transação do EF Core
     - Salvar Order e OrderItems
     - Criar e salvar OutboxMessage com evento `OrderCreated`
     - Commit da transação (garante atomicidade)
   - **NÃO** publicar diretamente no RabbitMQ no endpoint

7. **Migrations:**
   ```bash
   dotnet ef migrations add InitialCreate -p src/Orders.Api
   ```
   A migration deve incluir tabelas: Orders, OrderItems e OutboxMessages

### Entregáveis:
- Orders.Api funcional com padrão Outbox
- Integração com Catalog.Api via Service Discovery
- Validação de produtos
- Eventos salvos na tabela Outbox (transação ACID)
- **Observação**: Eventos ainda não são publicados no RabbitMQ (isso será feito na Fase 4.5)

---

## Fase 4.5: Outbox Processor (Background Service)

### Tarefas:

1. **Criar Background Service no Orders.Api:**
   ```bash
   # Criar classe OutboxProcessor.cs na pasta Services/
   ```

2. **Implementar OutboxProcessor:**
   - Herdar de `BackgroundService`
   - Executar em loop contínuo (intervalo configurável, ex: 5 segundos)
   - Lógica do processamento:
     - Buscar mensagens não processadas (`IsProcessed = false`)
     - Ordenar por `CreatedAt` (FIFO)
     - Limitar quantidade por batch (ex: 10 mensagens)
     - Para cada mensagem:
       - Publicar no RabbitMQ
       - Se sucesso: marcar `IsProcessed = true`, `ProcessedAt = DateTime.UtcNow`
       - Se falha: logar erro e continuar (retry na próxima execução)

3. **Implementar RabbitMQ Publisher:**
   - Criar `IRabbitMqPublisher` e `RabbitMqPublisher`
   - Métodos: `PublishAsync(string eventType, string payload)`
   - Configuração de conexão via appsettings/Aspire
   - Tratamento de erros e reconexão

4. **Configurações:**
   - Registrar `OutboxProcessor` como Hosted Service:
     ```csharp
     builder.Services.AddHostedService<OutboxProcessor>();
     ```
   - Configurar intervalo de processamento (appsettings.json):
     ```json
     "OutboxProcessor": {
       "IntervalSeconds": 5,
       "BatchSize": 10
     }
     ```

5. **Idempotência e Atomicidade:**
   - Usar transação ao marcar mensagem como processada
   - Garantir que a mesma mensagem não seja publicada duas vezes
   - Implementar retry com backoff exponencial (opcional)

6. **Logging e Observabilidade:**
   - Logar início/fim do processamento de cada batch
   - Logar erros de publicação com detalhes
   - Métricas: mensagens processadas, erros, latência

7. **Health Check:**
   - Adicionar health check para RabbitMQ
   - Monitorar se o processador está ativo

### Implementação Exemplo:

```csharp
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly int _intervalSeconds;
    private readonly int _batchSize;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

                var messages = await dbContext.OutboxMessages
                    .Where(m => !m.IsProcessed)
                    .OrderBy(m => m.CreatedAt)
                    .Take(_batchSize)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    await publisher.PublishAsync(message.EventType, message.Payload);

                    message.IsProcessed = true;
                    message.ProcessedAt = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Processed {Count} outbox messages", messages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }
}
```

### Entregáveis:
- OutboxProcessor funcional como Background Service
- Publicação confiável de eventos no RabbitMQ
- Retry automático em caso de falhas
- Idempotência no processamento
- Logging e observabilidade completos
- Health check para RabbitMQ

---

## Fase 5: Worker Notifications.Worker (com Idempotência)

### Tarefas:

1. **Criar Worker Service**
   ```bash
   dotnet new worker -n Notifications.Worker -o src/Notifications.Worker
   dotnet sln add src/Notifications.Worker
   dotnet add src/Notifications.Worker reference src/MicroservicesAspire.ServiceDefaults
   ```

2. **Adicionar Pacotes NuGet:**
   - RabbitMQ.Client
   - Microsoft.Extensions.Caching.Memory (para controle de duplicatas)

3. **Implementar Consumer com Idempotência:**
   - Background Service que consome fila do RabbitMQ
   - Processa eventos `OrderCreated`
   - **Idempotência**: Garantir que a mesma mensagem não seja processada duas vezes
     - Opção 1: Cache em memória de OrderIds processados (TTL de 1 hora)
     - Opção 2: Tabela de eventos processados no banco (mais robusto)
     - Verificar se OrderId já foi processado antes de executar a lógica
   - Loga notificação estruturada (simulação)
   - Fazer acknowledge (ACK) da mensagem apenas após processamento bem-sucedido

4. **Logging:**
   - Usar Serilog para logs estruturados
   - Incluir OrderId, UserId, TotalAmount nos logs
   - Log levels apropriados (Information para sucesso, Error para falhas)
   - Logar mensagens duplicadas (já processadas) como Warning

5. **Tratamento de Erros:**
   - Implementar retry com backoff exponencial
   - Dead Letter Queue para mensagens com falha após N tentativas
   - Logar erros detalhados para troubleshooting

### Implementação Exemplo (Idempotência com Cache):

```csharp
public class NotificationConsumer : BackgroundService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<NotificationConsumer> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Conectar ao RabbitMQ e consumir eventos OrderCreated
        var message = ConsumeFromQueue(); // Deserializar JSON

        var orderId = message.OrderId;

        // Verificar se já foi processado (idempotência)
        if (_cache.TryGetValue($"processed:{orderId}", out _))
        {
            _logger.LogWarning("Order {OrderId} already processed, skipping", orderId);
            channel.BasicAck(deliveryTag, false);
            return;
        }

        // Processar notificação
        _logger.LogInformation("Processing notification for Order {OrderId}", orderId);

        // Marcar como processado (TTL de 1 hora)
        _cache.Set($"processed:{orderId}", true, TimeSpan.FromHours(1));

        // ACK apenas após sucesso
        channel.BasicAck(deliveryTag, false);
    }
}
```

### Entregáveis:
- Worker funcional consumindo RabbitMQ
- Idempotência implementada (sem processamento duplicado)
- Logging estruturado
- Tratamento de erros com retry
- ACK apenas após sucesso

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

### Mensageria e Padrões:
- Transactional Outbox Pattern
- Background Services (Hosted Services)
- At-Least-Once Delivery
- Idempotência (Memory Cache / Database)

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
11. **Transactional Outbox Pattern**: Consistência entre persistência de dados e publicação de eventos
12. **Idempotência**: Processamento seguro de mensagens duplicadas
13. **At-Least-Once Delivery**: Garantia de entrega de eventos com retry automático
14. **Background Processing**: Processamento assíncrono de eventos desacoplado da API

---

## Cronograma Estimado

| Fase | Estimativa | Complexidade |
|------|-----------|--------------|
| Fase 1: Git Init | 5 min | Baixa |
| Fase 2: Aspire Base | 15 min | Média |
| Fase 3: Catalog.Api | 45 min | Alta |
| Fase 4: Orders.Api (com Outbox) | 75 min | Alta |
| Fase 4.5: Outbox Processor | 25 min | Média-Alta |
| Fase 5: Notifications.Worker (com Idempotência) | 35 min | Média-Alta |
| Fase 6: AppHost Config | 20 min | Média |
| Fase 7: Documentação | 30 min | Baixa |
| Fase 8: Testes | 25 min | Média |
| **TOTAL** | **~4.25 horas** | - |

### Notas sobre Cronograma:
- **Fase 4**: +15 min para implementar tabela Outbox e lógica transacional
- **Fase 4.5 (Nova)**: +25 min para implementar Background Service de processamento
- **Fase 5**: +5 min para implementar idempotência no consumer
- **Fase 8**: +5 min para testar cenários do padrão Outbox
- **Complexidade aumentada**: O padrão Outbox adiciona complexidade, mas garante consistência transacional

---

## Próximos Passos

Após confirmar este plano, a implementação seguirá a ordem das fases descritas acima, começando pela inicialização do Git e criação da estrutura base do Aspire.

**Pronto para começar?** 🚀
