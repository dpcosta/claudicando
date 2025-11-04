# Plano de Deploy: .NET Aspire → GitHub Container Registry → Dokploy

**Projeto:** claudicando - Sistema de Microserviços com .NET Aspire
**Objetivo:** Implementar pipeline completo de CI/CD para aplicação .NET Aspire usando GitHub Container Registry como repositório de imagens Docker e Dokploy como plataforma de hospedagem.

---

## 📋 Resumo Executivo da Solução

### Serviços a serem deployados
1. **Catalog.Api** - API de catálogo de produtos (Minimal API + EF Core + Redis cache)
2. **Orders.Api** - API de pedidos com Transactional Outbox Pattern (Minimal API + EF Core + RabbitMQ + OutboxProcessor)
3. **Web.Blazor** - Admin Dashboard Blazor Web App (SSR + Interactive Server)

### Infraestrutura Necessária
- **SQL Server**: 2 bancos (`catalogdb`, `ordersdb`)
- **Redis**: Cache distribuído (Catalog.Api)
- **RabbitMQ**: Message broker com management plugin (Orders.Api)

### Dependências entre Serviços
- **Orders.Api → Catalog.Api**: Validação de produtos via Service Discovery (HTTP)
- **Web.Blazor (Admin Dashboard) → Catalog.Api**: Exibição de produtos via Service Discovery (HTTP)
- **Orders.Api → RabbitMQ**: Publicação de eventos via Transactional Outbox Pattern (AMQP)

### Padrões Arquiteturais Implementados
- ✅ Transactional Outbox Pattern (Orders.Api)
- ✅ Repository Pattern (ambas APIs)
- ✅ Service Discovery (.NET Aspire ServiceDefaults)
- ✅ Cache Distribuído (Redis no Catalog.Api)
- ✅ Event-Driven Architecture (RabbitMQ)
- ✅ UUID v7 para geração de IDs (performance)
- ✅ OpenTelemetry (traces, metrics, logs)

### Imagens Docker (GHCR)
```
ghcr.io/<usuario>/claudicando-catalog-api:latest
ghcr.io/<usuario>/claudicando-orders-api:latest
ghcr.io/<usuario>/claudicando-admin-dashboard:latest
```

### Domínios Sugeridos
- `catalog-api.claudicando.net.br` → Catalog.Api
- `orders-api.claudicando.net.br` → Orders.Api
- `dashboard.claudicando.net.br` → Web.Blazor (Admin Dashboard)

---

## Fase 1: Preparação do Projeto Aspire

### 1.1 Estrutura do Projeto
- [x] Projeto Aspire funcional localmente (MicroservicesAspire.AppHost + ServiceDefaults)
- [x] **Serviços a serem containerizados**:
  - **Catalog.Api**: API de catálogo de produtos (Minimal API + EF Core + Redis cache)
  - **Orders.Api**: API de pedidos com Transactional Outbox Pattern (Minimal API + EF Core + RabbitMQ)
  - **Web.Blazor**: Admin Dashboard Blazor Web App (SSR + Interactive Server)
- [x] **Dependências externas**:
  - **SQL Server**: 2 bancos de dados (`catalogdb`, `ordersdb`)
  - **Redis**: Cache distribuído (usado por Catalog.Api)
  - **RabbitMQ**: Message broker com management plugin (exchange: `orders-events`)
- [x] **Arquitetura documentada**:
  - Orders.Api → Catalog.Api (Service Discovery para validação de produtos)
  - Web.Blazor (Admin Dashboard) → Catalog.Api (Service Discovery para exibição de produtos)
  - Orders.Api → RabbitMQ (publicação de eventos via OutboxProcessor)

### 1.2 Dockerfiles
- [ ] Criar Dockerfiles para os 3 serviços:
  - [ ] `src/Catalog.Api/Dockerfile`
  - [ ] `src/Orders.Api/Dockerfile`
  - [ ] `src/Web.Blazor/Dockerfile`
- [ ] Usar imagens base otimizadas (.NET 10 runtime - framework atual do projeto)
- [ ] Configurar multi-stage builds para reduzir tamanho das imagens
- [ ] Definir variáveis de ambiente necessárias em cada Dockerfile:
  - `ASPNETCORE_ENVIRONMENT`
  - `ASPNETCORE_URLS=http://+:8080`
  - Connection strings via secrets/env vars
- [ ] Adicionar `.dockerignore` na raiz para otimizar contexto de build

### 1.3 Manifesto Aspire
- [ ] Gerar o manifesto do Aspire: `dotnet run --project AppHost --publisher manifest --output-path aspire-manifest.json`
- [ ] Revisar o manifesto para entender as dependências e configurações de cada serviço
- [ ] Identificar configurações que precisam ser ajustadas para produção

---

## Fase 2: Configuração do GitHub Container Registry

### 2.1 Personal Access Token (PAT)
- [ ] Acessar GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
- [ ] Criar um PAT com permissões: `write:packages`, `read:packages`, `delete:packages`
- [ ] Armazenar o token de forma segura (você precisará dele no CI/CD e no Dokploy)
- [ ] Documentar data de expiração do token e processo de renovação

### 2.2 GitHub Actions - Workflow CI/CD
- [ ] Criar estrutura de diretórios: `.github/workflows/`
- [ ] Criar arquivo `build-and-push.yml`
- [ ] Configurar triggers:
  - Push em branch `main` ou `master`
  - Tags de release (ex: `v*.*.*`)
  - Pull requests (apenas build, sem push)
- [ ] Definir jobs para:
  - Build de cada serviço
  - Testes unitários e integração (opcional mas recomendado)
  - Push das imagens para GHCR com tags apropriadas

### 2.3 Nomenclatura das Imagens
- [ ] Definir padrão de nomenclatura consistente
- [ ] Formato recomendado: `ghcr.io/<seu-usuario>/claudicando-<servico>:<tag>`
- [ ] **Imagens do projeto claudicando**:
  - `ghcr.io/<usuario>/claudicando-catalog-api:latest`
  - `ghcr.io/<usuario>/claudicando-catalog-api:v1.0.0`
  - `ghcr.io/<usuario>/claudicando-orders-api:latest`
  - `ghcr.io/<usuario>/claudicando-orders-api:v1.0.0`
  - `ghcr.io/<usuario>/claudicando-admin-dashboard:latest`
  - `ghcr.io/<usuario>/claudicando-admin-dashboard:v1.0.0`
- [ ] Tags por ambiente:
  - `latest`: Produção estável
  - `dev`: Development/feature branches
  - `staging`: Ambiente de staging
  - `v1.0.0`: Semantic versioning
  - `<commit-sha>`: Rastreabilidade por commit

### 2.4 Secrets do GitHub
- [ ] Verificar se `GITHUB_TOKEN` automático tem permissões suficientes
- [ ] Configurar secrets adicionais no repositório se necessário
- [ ] Testar acesso ao GHCR com as credenciais configuradas

---

## Fase 3: Build e Push das Imagens

### 3.1 Workflow de Build
- [ ] Implementar workflow completo no GitHub Actions
- [ ] Configurar login no GHCR: `docker login ghcr.io -u $GITHUB_ACTOR -p $GITHUB_TOKEN`
- [ ] Construir imagens com tags múltiplas:
  - `latest` para última versão estável
  - Tag de versão específica (ex: `v1.0.0`)
  - SHA do commit para rastreabilidade
- [ ] Otimizar cache de layers do Docker
- [ ] Push para o GHCR de todas as imagens

### 3.2 Validação
- [ ] Verificar imagens publicadas em `https://github.com/<usuario>?tab=packages`
- [ ] Confirmar visibilidade das imagens (públicas ou privadas)
- [ ] Testar pull de imagens localmente para validação
- [ ] Verificar tags e metadados das imagens

### 3.3 Versionamento
- [ ] Definir estratégia de versionamento (Semantic Versioning recomendado)
- [ ] Configurar geração automática de tags baseada em commits ou releases
- [ ] Usar tags de commit SHA para rastreabilidade completa
- [ ] Documentar convenção de tags no README do projeto

---

## Fase 4: Configuração do Dokploy

### 4.1 Preparação do Servidor
- [ ] Garantir que Dokploy está instalado e rodando corretamente
- [ ] Verificar recursos disponíveis do servidor:
  - CPU: mínimo recomendado para sua stack
  - RAM: calcular baseado nos serviços
  - Disco: considerar volumes e logs
- [ ] Configurar domínios/subdomínios no DNS apontando para IP do servidor
- [ ] Verificar portas abertas no firewall

### 4.2 Conexão com GHCR
- [ ] No Dokploy, acessar configurações de Registry
- [ ] Adicionar novo registry com informações:
  - Registry URL: `ghcr.io`
  - Username: seu username do GitHub
  - Password/Token: PAT criado anteriormente
- [ ] Testar conexão com o registry
- [ ] Verificar permissões de pull das imagens

### 4.3 Banco de Dados SQL Server
- [ ] Decidir estratégia de banco de dados:
  - **Opção A:** SQL Server no próprio Dokploy (Docker)
  - **Opção B:** SQL Server externo (Azure SQL, AWS RDS, servidor dedicado)
- [ ] Se opção A (Docker no Dokploy):
  - [ ] Criar container do SQL Server (imagem oficial Microsoft)
  - [ ] Configurar volume persistente para dados
  - [ ] Definir senha forte do SA
  - [ ] Configurar backup automático
- [ ] Se opção B (externo):
  - [ ] Configurar connection string para acesso externo
  - [ ] Verificar regras de firewall e segurança
- [ ] Testar conectividade do servidor Dokploy ao SQL Server
- [ ] Documentar connection strings e credenciais (em secret manager)

### 4.4 Serviços Dependentes

#### Redis (Cache)
- [ ] Criar container Redis no Dokploy
- [ ] Configurar volume persistente: `/data`
- [ ] Expor porta interna: `6379`
- [ ] **Uso**: Cache distribuído do Catalog.Api
  - Cache de lista de produtos (TTL: 5 minutos)
  - Invalidação ao criar/atualizar/deletar produtos
- [ ] Testar conectividade: `docker exec catalog-api redis-cli -h redis ping`
- [ ] Documentar connection string: `redis:6379`

#### RabbitMQ (Message Broker)
- [ ] Criar container RabbitMQ no Dokploy (imagem: `rabbitmq:management`)
- [ ] Configurar volumes persistentes:
  - `/var/lib/rabbitmq`
- [ ] Expor portas:
  - `5672`: AMQP protocol
  - `15672`: Management UI (opcional, apenas para debug)
- [ ] Definir variáveis de ambiente:
  - `RABBITMQ_DEFAULT_USER=<usuario>`
  - `RABBITMQ_DEFAULT_PASS=<senha-forte>`
- [ ] **Configuração do Orders.Api**:
  - Exchange: `orders-events` (tipo: `topic`)
  - Eventos publicados: `OrderCreated`
  - Background Service: OutboxProcessor (5s interval, batch 10)
- [ ] Testar conectividade: Verificar logs do Orders.Api para conexão bem-sucedida
- [ ] Documentar connection string: `amqp://<user>:<pass>@rabbitmq:5672`

#### Checklist Geral
- [ ] Todos os serviços na mesma Docker network
- [ ] Volumes configurados para persistência
- [ ] Health checks configurados
- [ ] Credenciais armazenadas em secrets (não hardcoded)

---

## Fase 5: Deploy dos Serviços no Dokploy

### 5.1 Criar Aplicações
- [ ] Criar 3 aplicações no Dokploy:

#### Catalog.Api
- [ ] **Name**: `claudicando-catalog-api`
- [ ] **Type**: Docker Image (external)
- [ ] **Image**: `ghcr.io/<usuario>/claudicando-catalog-api:latest`
- [ ] **Pull Policy**: Always
- [ ] **Resources**: CPU: 0.5 cores, Memory: 512MB (ajustar conforme carga)
- [ ] **Port**: 8080 (interno)

#### Orders.Api
- [ ] **Name**: `claudicando-orders-api`
- [ ] **Type**: Docker Image (external)
- [ ] **Image**: `ghcr.io/<usuario>/claudicando-orders-api:latest`
- [ ] **Pull Policy**: Always
- [ ] **Resources**: CPU: 0.5 cores, Memory: 512MB (ajustar conforme carga)
- [ ] **Port**: 8080 (interno)
- [ ] **Nota**: Contém OutboxProcessor como HostedService (não precisa de container separado)

#### Web.Blazor (Admin Dashboard)
- [ ] **Name**: `claudicando-admin-dashboard`
- [ ] **Type**: Docker Image (external)
- [ ] **Image**: `ghcr.io/<usuario>/claudicando-admin-dashboard:latest`
- [ ] **Pull Policy**: Always
- [ ] **Resources**: CPU: 0.5 cores, Memory: 512MB (ajustar conforme carga)
- [ ] **Port**: 8080 (interno)

### 5.2 Configuração de Variáveis de Ambiente

#### Catalog.Api
- [ ] **Variáveis comuns**:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ASPNETCORE_URLS=http://+:8080`
  - `OTEL_SERVICE_NAME=claudicando-catalog-api`
- [ ] **Connection Strings**:
  - `ConnectionStrings__catalogdb=Server=sqlserver;Database=catalogdb;User Id=sa;Password=<senha>;TrustServerCertificate=true`
  - `ConnectionStrings__redis=redis:6379`
- [ ] **Logging**:
  - `Logging__LogLevel__Default=Information`
  - `Logging__LogLevel__Microsoft.AspNetCore=Warning`

#### Orders.Api
- [ ] **Variáveis comuns**:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ASPNETCORE_URLS=http://+:8080`
  - `OTEL_SERVICE_NAME=claudicando-orders-api`
- [ ] **Connection Strings**:
  - `ConnectionStrings__ordersdb=Server=sqlserver;Database=ordersdb;User Id=sa;Password=<senha>;TrustServerCertificate=true`
  - `ConnectionStrings__rabbitmq=amqp://<user>:<pass>@rabbitmq:5672`
- [ ] **Service Discovery** (comunicação com Catalog.Api):
  - `services__catalog-api__http__0=http://claudicando-catalog-api:8080`
- [ ] **Configurações do OutboxProcessor**:
  - `OutboxProcessor__IntervalSeconds=5`
  - `OutboxProcessor__BatchSize=10`
- [ ] **RabbitMQ**:
  - `RabbitMQ__ExchangeName=orders-events`
  - `RabbitMQ__ExchangeType=topic`
- [ ] **Logging**:
  - `Logging__LogLevel__Default=Information`
  - `Logging__LogLevel__Microsoft.AspNetCore=Warning`
  - `Logging__LogLevel__Orders.Api.Services.OutboxProcessor=Information`

#### Web.Blazor (Admin Dashboard)
- [ ] **Variáveis comuns**:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ASPNETCORE_URLS=http://+:8080`
  - `OTEL_SERVICE_NAME=claudicando-admin-dashboard`
- [ ] **Service Discovery** (comunicação com Catalog.Api):
  - `services__catalog-api__http__0=http://claudicando-catalog-api:8080`
- [ ] **Logging**:
  - `Logging__LogLevel__Default=Information`
  - `Logging__LogLevel__Microsoft.AspNetCore=Warning`

#### ⚠️ Importante
- Usar secrets do Dokploy para senhas e credenciais sensíveis
- Nunca hardcoded em variáveis de ambiente visíveis
- Documentar todas as variáveis em secret manager

### 5.3 Networking
- [ ] Criar network compartilhada no Dokploy: `claudicando-network`
- [ ] Conectar todos os containers à mesma network:
  - `claudicando-catalog-api`
  - `claudicando-orders-api`
  - `claudicando-admin-dashboard`
  - `sqlserver`
  - `redis`
  - `rabbitmq`
- [ ] **Configurar Service Discovery** usando nomes dos containers:
  - Orders.Api → Catalog.Api: `http://claudicando-catalog-api:8080`
  - Admin Dashboard → Catalog.Api: `http://claudicando-catalog-api:8080`
- [ ] **Comunicação entre serviços**:
  - Catalog.Api acessa: `sqlserver` (DB), `redis` (cache)
  - Orders.Api acessa: `sqlserver` (DB), `rabbitmq` (eventos), `claudicando-catalog-api` (validação)
  - Admin Dashboard acessa: `claudicando-catalog-api` (dados)
- [ ] Testar conectividade interna:
  ```bash
  # Testar Orders.Api → Catalog.Api
  docker exec claudicando-orders-api wget -qO- http://claudicando-catalog-api:8080/health

  # Testar Admin Dashboard → Catalog.Api
  docker exec claudicando-admin-dashboard wget -qO- http://claudicando-catalog-api:8080/health

  # Testar Catalog.Api → Redis
  docker exec claudicando-catalog-api redis-cli -h redis ping
  ```

### 5.4 Portas e Exposição
- [ ] Mapear portas internas (8080) para externas via reverse proxy do Dokploy
- [ ] **Catalog.Api** (API pública):
  - [ ] Configurar reverse proxy do Dokploy
  - [ ] Definir subdomínio: `catalog-api.claudicando.net.br`
  - [ ] Configurar SSL/TLS com Let's Encrypt
  - [ ] Endpoints públicos:
    - `GET /api/products` - Lista produtos
    - `GET /api/products/{id}` - Busca produto
    - `POST /api/products` - Cria produto
    - `PUT /api/products/{id}` - Atualiza produto
    - `DELETE /api/products/{id}` - Remove produto
    - `GET /health` - Health check
    - `GET /swagger` - Documentação Swagger (opcional em produção)
- [ ] **Orders.Api** (API pública):
  - [ ] Configurar reverse proxy do Dokploy
  - [ ] Definir subdomínio: `orders-api.claudicando.net.br`
  - [ ] Configurar SSL/TLS com Let's Encrypt
  - [ ] Endpoints públicos:
    - `POST /api/orders` - Cria pedido
    - `GET /api/orders/{id}` - Consulta pedido
    - `GET /api/orders?userId={userId}` - Lista pedidos
    - `GET /health` - Health check
    - `GET /swagger` - Documentação Swagger (opcional em produção)
- [ ] **Admin Dashboard** (Frontend público):
  - [ ] Configurar reverse proxy do Dokploy
  - [ ] Definir domínio: `dashboard.claudicando.net.br`
  - [ ] Configurar SSL/TLS com Let's Encrypt
- [ ] **Serviços internos** (NÃO expor):
  - SQL Server (acessível apenas via network interna)
  - Redis (acessível apenas via network interna)
  - RabbitMQ (porta 5672 interna, UI 15672 opcional para debug via VPN/IP whitelist)

### 5.5 Health Checks
- [ ] Implementar endpoints de health check em cada serviço:
  - ASP.NET Core: usar `app.MapHealthChecks("/health")`
  - Adicionar checks de dependências (DB, Redis, etc.)
- [ ] Configurar health checks no Dokploy para cada container:
  - **Endpoint:** `/health` ou `/healthz`
  - **Interval:** 30s
  - **Timeout:** 5s
  - **Retries:** 3
- [ ] Definir restart policies:
  - **Policy:** `unless-stopped` ou `on-failure`
  - **Max retries:** 3

### 5.6 Volumes e Persistência
- [ ] Identificar dados que precisam persistir:
  - Logs da aplicação
  - Uploads de arquivos
  - Cache local
  - Dados temporários
- [ ] Criar volumes no Dokploy para cada necessidade
- [ ] Mapear volumes nos containers:
  - `/app/logs` → volume de logs
  - `/app/uploads` → volume de uploads
- [ ] Configurar rotação de logs
- [ ] Planejar estratégia de backup para volumes críticos

---

## Fase 6: Service Discovery e Aspire Dashboard

### 6.1 Aspire Dashboard (Opcional)
- [ ] Decidir se o Aspire Dashboard será deployado em produção
  - **Prós:** Monitoring unificado, visualização de telemetria, debug de dependências
  - **Contras:** Recurso adicional, precisa ser protegido adequadamente
- [ ] Se sim, criar aplicação no Dokploy para o Dashboard:
  - Image: `mcr.microsoft.com/dotnet/aspire-dashboard:9.0`
  - Configurar autenticação (senha ou token)
  - Expor apenas via VPN ou IP whitelisting
- [ ] Configurar acesso seguro (HTTPS obrigatório)
- [ ] Conectar serviços ao Dashboard via OTLP

### 6.2 Service Defaults
- [x] Todos os serviços usam `MicroservicesAspire.ServiceDefaults` (já implementado)
- [x] Configuração de telemetria em cada serviço:
  ```csharp
  builder.AddServiceDefaults(); // já presente em todos os Program.cs
  ```
- [x] **Funcionalidades incluídas no ServiceDefaults**:
  - Health checks padrão (`/health`, `/health/ready`, `/health/live`)
  - OpenTelemetry (traces, metrics, logs via OTLP)
  - Service Discovery (resolve nomes de serviços automaticamente)
  - Resilience (retry policies, circuit breakers)
- [ ] **Ajustar variáveis para produção no Dokploy**:
  - Health checks configurados automaticamente
  - OpenTelemetry endpoint (se usar backend externo): `OTEL_EXPORTER_OTLP_ENDPOINT=http://<grafana-ou-jaeger>:4317`
  - Service names já definidos nas variáveis de ambiente (Fase 5.2)

### 6.3 Comunicação entre Serviços

#### Fluxos de Comunicação da Solução

**1. Orders.Api → Catalog.Api (HTTP - Service Discovery)**
- [ ] **Propósito**: Validar produtos e estoque ao criar pedidos
- [ ] **Configuração no Dokploy**:
  ```env
  # Orders.Api environment
  services__catalog-api__http__0=http://claudicando-catalog-api:8080
  ```
- [ ] **Implementação**: `CatalogService` usa `HttpClient` com Service Discovery
- [ ] **Endpoint chamado**: `GET http://claudicando-catalog-api:8080/api/products/{id}`
- [ ] **Resilience**: Retry policies e circuit breakers (via ServiceDefaults)

**2. Admin Dashboard → Catalog.Api (HTTP - Service Discovery)**
- [ ] **Propósito**: Exibir produtos no dashboard administrativo
- [ ] **Configuração no Dokploy**:
  ```env
  # Admin Dashboard environment
  services__catalog-api__http__0=http://claudicando-catalog-api:8080
  ```
- [ ] **Implementação**: `HttpClient` injetado via DI com Service Discovery
- [ ] **Endpoint chamado**: `GET http://claudicando-catalog-api:8080/api/products`
- [ ] **Resilience**: Retry policies (via ServiceDefaults)

**3. Orders.Api → RabbitMQ (AMQP - Async)**
- [ ] **Propósito**: Publicar eventos de pedidos criados
- [ ] **Padrão**: Transactional Outbox Pattern
- [ ] **Background Service**: `OutboxProcessor`
  - Processa mensagens da tabela `OutboxMessages` a cada 5s
  - Batch de 10 mensagens por vez
  - Publica no exchange `orders-events` via `RabbitMqPublisher`
- [ ] **Configuração no Dokploy**:
  ```env
  ConnectionStrings__rabbitmq=amqp://<user>:<pass>@rabbitmq:5672
  RabbitMQ__ExchangeName=orders-events
  RabbitMQ__ExchangeType=topic
  ```
- [ ] **Eventos publicados**: `OrderCreated` (routing key: `order.created`)

#### Testes de Comunicação
- [ ] Testar Orders.Api → Catalog.Api:
  ```bash
  # Criar pedido e verificar logs de validação
  curl -X POST https://orders-api.claudicando.net.br/api/orders \
    -H "Content-Type: application/json" \
    -d '{"userId":"user123","items":[{"productId":"<id>","quantity":2}]}'
  ```
- [ ] Verificar logs do Orders.Api para conexão com Catalog.Api:
  ```
  Validating product <id> with Catalog.Api
  Product validation successful
  ```
- [ ] Verificar logs do OutboxProcessor para publicação no RabbitMQ:
  ```
  Processing 1 outbox messages
  Published message <id> to RabbitMQ
  ```
- [ ] Acessar RabbitMQ Management UI (se habilitado) e verificar exchange `orders-events`

---

## Fase 7: Validação e Testes

### 7.1 Smoke Tests

#### Catalog.Api
- [ ] Testar health check: `curl https://catalog-api.claudicando.net.br/health`
- [ ] Testar listagem de produtos: `curl https://catalog-api.claudicando.net.br/api/products`
- [ ] Testar Swagger (se habilitado): `https://catalog-api.claudicando.net.br/swagger`
- [ ] Verificar logs no Dokploy:
  - ✅ Conexão com `catalogdb` bem-sucedida
  - ✅ Conexão com `redis` bem-sucedida
  - ✅ Sem erros críticos no startup
- [ ] Testar cache Redis:
  - Fazer 2 requisições seguidas em `/api/products`
  - Segunda requisição deve ser mais rápida (cache hit)
  - Verificar logs: `Cache hit for products` ou `Cache miss for products`

#### Orders.Api
- [ ] Testar health check: `curl https://orders-api.claudicando.net.br/health`
- [ ] Testar criação de pedido: `curl -X POST https://orders-api.claudicando.net.br/api/orders -H "Content-Type: application/json" -d '{"userId":"test","items":[...]}'`
- [ ] Testar Swagger (se habilitado): `https://orders-api.claudicando.net.br/swagger`
- [ ] Verificar logs no Dokploy:
  - ✅ Conexão com `ordersdb` bem-sucedida
  - ✅ Conexão com `rabbitmq` bem-sucedida
  - ✅ Service Discovery resolveu `claudicando-catalog-api`
  - ✅ OutboxProcessor iniciado: `OutboxProcessor started`
  - ✅ Sem erros críticos no startup
- [ ] Verificar OutboxProcessor:
  - Criar pedido e verificar logs: `Processing X outbox messages`
  - Verificar que mensagem foi marcada como processada na tabela `OutboxMessages`

#### Admin Dashboard
- [ ] Testar acesso ao dashboard: `https://dashboard.claudicando.net.br`
- [ ] Verificar que página de produtos carrega corretamente
- [ ] Verificar logs no Dokploy:
  - ✅ Service Discovery resolveu `claudicando-catalog-api`
  - ✅ Sem erros críticos no startup
- [ ] Testar navegação e interatividade Blazor Server

#### Infraestrutura
- [ ] SQL Server:
  - Bancos `catalogdb` e `ordersdb` criados
  - Migrations aplicadas corretamente
  - Conexões ativas visíveis nos logs
- [ ] Redis:
  - Container rodando
  - Catalog.Api conectado
- [ ] RabbitMQ:
  - Container rodando
  - Exchange `orders-events` criado
  - Orders.Api conectado

### 7.2 Testes de Integração

#### Fluxo Completo: Criar e Consultar Produto
- [ ] **Criar produto no Catalog.Api**:
  ```bash
  curl -X POST https://catalog-api.claudicando.net.br/api/products \
    -H "Content-Type: application/json" \
    -d '{"name":"Notebook","description":"Dell Inspiron","price":3500.00,"stock":10}'
  ```
- [ ] **Consultar produto criado**:
  ```bash
  curl https://catalog-api.claudicando.net.br/api/products/{id}
  ```
- [ ] **Visualizar no dashboard**: Acessar Admin Dashboard e verificar produto na lista

#### Fluxo Completo: Criar Pedido com Validação
- [ ] **Criar pedido no Orders.Api** (deve validar produto no Catalog.Api):
  ```bash
  curl -X POST https://orders-api.claudicando.net.br/api/orders \
    -H "Content-Type: application/json" \
    -d '{
      "userId":"user123",
      "items":[
        {"productId":"{id-do-produto}","quantity":2}
      ]
    }'
  ```
- [ ] **Verificar logs do Orders.Api**:
  - Validação com Catalog.Api bem-sucedida
  - Pedido criado e salvo em `ordersdb`
  - Mensagem inserida na tabela `OutboxMessages`
- [ ] **Aguardar OutboxProcessor processar** (máximo 5 segundos)
- [ ] **Verificar logs do OutboxProcessor**:
  - Mensagem processada
  - Evento publicado no RabbitMQ exchange `orders-events`
  - Mensagem marcada como `IsProcessed=true`
- [ ] **Consultar pedido criado**:
  ```bash
  curl https://orders-api.claudicando.net.br/api/orders/{order-id}
  ```

#### Teste de Persistência
- [ ] Criar dados (produtos e pedidos)
- [ ] Reiniciar containers via Dokploy:
  - `claudicando-catalog-api`
  - `claudicando-orders-api`
  - `claudicando-admin-dashboard`
- [ ] Verificar que dados persistem após reinicialização
- [ ] Verificar que volumes do SQL Server estão funcionando

#### Teste de Cache Redis
- [ ] Listar produtos (primeira requisição - cache miss)
- [ ] Listar produtos novamente (cache hit - mais rápido)
- [ ] Criar/atualizar produto (invalida cache)
- [ ] Listar produtos (cache miss novamente)

#### Teste de Event-Driven Architecture
- [ ] Criar pedido
- [ ] Verificar na tabela `OutboxMessages` que mensagem foi criada
- [ ] Aguardar processamento (5s)
- [ ] Verificar que `IsProcessed=true` e `ProcessedAt` tem timestamp
- [ ] Verificar no RabbitMQ Management UI que evento foi publicado no exchange `orders-events`

### 7.3 Monitoramento
- [ ] Configurar acesso aos logs agregados no Dokploy
- [ ] Verificar métricas de saúde dos containers:
  - CPU usage
  - Memory usage
  - Network I/O
  - Disk I/O
- [ ] Configurar alertas (se disponível no Dokploy):
  - Container crashed
  - High CPU/Memory
  - Failed health checks
- [ ] Testar Aspire Dashboard (se deployado)

### 7.4 Performance
- [ ] Executar testes de carga básicos:
  - Usar ferramentas como k6, Apache JMeter, ou Artillery
  - Simular carga normal esperada
  - Identificar gargalos
- [ ] Verificar uso de recursos durante carga:
  - CPU não deve estar constantemente em 100%
  - Memory não deve ter leaks
  - Tempo de resposta aceitável
- [ ] Ajustar limites de recursos se necessário:
  - Aumentar CPU/Memory limits
  - Configurar horizontal scaling (se Dokploy suportar)

---

## Fase 8: Automação de Deploy

### 8.1 Webhook do Dokploy
- [ ] No Dokploy, gerar webhook URL para cada aplicação
- [ ] Webhook permite re-deploy automático quando nova imagem é disponibilizada
- [ ] Copiar URLs dos webhooks
- [ ] Adicionar webhook URLs nos secrets do GitHub:
  - `DOKPLOY_WEBHOOK_API`
  - `DOKPLOY_WEBHOOK_WEB`
  - `DOKPLOY_WEBHOOK_WORKER`

### 8.2 GitHub Actions - Deploy Automático
- [ ] Adicionar step no workflow para chamar webhooks do Dokploy para cada serviço:
  ```yaml
  - name: Trigger Dokploy Deploy - Catalog.Api
    run: |
      curl -X POST ${{ secrets.DOKPLOY_WEBHOOK_CATALOG_API }}

  - name: Trigger Dokploy Deploy - Orders.Api
    run: |
      curl -X POST ${{ secrets.DOKPLOY_WEBHOOK_ORDERS_API }}

  - name: Trigger Dokploy Deploy - Admin Dashboard
    run: |
      curl -X POST ${{ secrets.DOKPLOY_WEBHOOK_ADMIN_DASHBOARD }}
  ```
- [ ] Configurar secrets no GitHub:
  - `DOKPLOY_WEBHOOK_CATALOG_API`
  - `DOKPLOY_WEBHOOK_ORDERS_API`
  - `DOKPLOY_WEBHOOK_ADMIN_DASHBOARD`
- [ ] Configurar deploy automático apenas para branch `main`
- [ ] **Ordem de deploy recomendada** (para evitar quebras):
  1. Catalog.Api (independente)
  2. Orders.Api (depende do Catalog.Api)
  3. Admin Dashboard (depende do Catalog.Api)
- [ ] Implementar estratégia de deploy:
  - **Rolling:** atualiza containers um por vez (recomendado para Dokploy)
  - Aguardar health check passar antes de prosseguir para próximo serviço
- [ ] Adicionar notificações de deploy (Discord, Slack, email)

### 8.3 Rollback
- [ ] Definir processo de rollback manual:
  1. Identificar última versão estável (tag da imagem)
  2. No Dokploy, atualizar image tag para versão anterior
  3. Re-deploy da aplicação
- [ ] Documentar comandos para rollback:
  ```bash
  # Via Dokploy UI
  # Ou via API/CLI se disponível
  ```
- [ ] Manter pelo menos 3 últimas versões de imagens no GHCR
- [ ] Testar processo de rollback em ambiente de staging primeiro
- [ ] Configurar backup automático antes de cada deploy (opcional)

---

## Fase 9: Documentação e Manutenção

### 9.1 Documentação
- [ ] Criar documento centralizado com:
  - URLs de todos os serviços e endpoints públicos
  - Credenciais de acesso (em vault/secret manager)
  - Arquitetura e diagrama de dependências
- [ ] Guia de troubleshooting comum:
  - Container não inicia: verificar logs, variáveis de ambiente
  - Erro de conexão ao DB: verificar connection string, firewall
  - Erro de comunicação entre serviços: verificar network, DNS
- [ ] Documentar variáveis de ambiente e configurações de cada serviço
- [ ] README atualizado com instruções de deploy

### 9.2 Backup e Disaster Recovery
- [ ] Configurar backup do SQL Server:
  - Backup completo diário
  - Backup transacional (se necessário)
  - Retenção de 7-30 dias
  - Testar restore regularmente
- [ ] Backup de volumes importantes:
  - Uploads de usuários
  - Configurações customizadas
  - Certificados SSL
- [ ] Documentar processo de restore completo:
  1. Provisionar novo servidor
  2. Instalar Dokploy
  3. Restaurar volumes do backup
  4. Restaurar banco de dados
  5. Re-deploy das aplicações
- [ ] Manter cópia dos backups em local externo (S3, Azure Blob, etc.)

### 9.3 Monitoramento Contínuo
- [ ] Estabelecer rotina de verificação de logs (diária ou semanal)
- [ ] Monitorar uso de recursos do servidor:
  - Alertas quando uso > 80%
  - Planejar upgrade de recursos
- [ ] Acompanhar métricas de negócio (se aplicável):
  - Número de requisições
  - Tempo médio de resposta
  - Taxa de erro
- [ ] Planejar escalabilidade futura:
  - Identificar serviços que precisarão escalar
  - Avaliar necessidade de load balancer
  - Considerar migração para Kubernetes (se crescer muito)

---

## Considerações Importantes

### Segurança
- ⚠️ **Secrets Management**: Usar secrets do Dokploy para:
  - Senha do SQL Server
  - Credenciais do RabbitMQ
  - Connection strings com senhas
  - Nunca hardcoded em código ou variáveis de ambiente visíveis
- ⚠️ **HTTPS Obrigatório**: Configurar Let's Encrypt para:
  - `catalog-api.claudicando.net.br` (Catalog.Api)
  - `orders-api.claudicando.net.br` (Orders.Api)
  - `dashboard.claudicando.net.br` (Admin Dashboard)
- ⚠️ **Aspire Dashboard**: Apenas em development (NÃO deployar em produção)
- ⚠️ **Imagens Docker**: Manter atualizadas (.NET 10 runtime + patches de segurança)
- ⚠️ **Rate Limiting**: Implementar em Catalog.Api e Orders.Api (futuro)
- ⚠️ **CORS**: Configurado adequadamente no Admin Dashboard (origem específica, não `*`)
- ⚠️ **Swagger**: Desabilitar em produção ou proteger com autenticação
- ⚠️ **RabbitMQ Management UI**: Expor apenas via VPN ou IP whitelisting (porta 15672)

### Networking
- 🌐 **Service Discovery Crítico**: Orders.Api e Admin Dashboard dependem do Catalog.Api
  - Usar nomes de containers: `claudicando-catalog-api`, `claudicando-orders-api`, `claudicando-admin-dashboard`
  - Configurar variáveis: `services__catalog-api__http__0=http://claudicando-catalog-api:8080`
- 🌐 **Comunicação Interna**: Todos os serviços na mesma Docker network (`claudicando-network`)
- 🌐 **Ordem de Inicialização**: Catalog.Api → Orders.Api → Admin Dashboard
- 🌐 **Testar Conectividade**: Usar `wget`, `curl` ou `ping` entre containers antes de validar deployment
- 🌐 **DNS Interno**: Sempre usar nomes de containers, nunca IPs (que podem mudar)

### SQL Server
- 🗄️ **Dois Bancos de Dados**: `catalogdb` (produtos) e `ordersdb` (pedidos + outbox)
- 🗄️ **SQL Server no Docker** requer atenção especial:
  - Volumes persistentes obrigatórios (`/var/opt/mssql`)
  - Configuração adequada de memória (mínimo 2GB)
  - Performance pode não ser ideal comparado a VM dedicada
- 🗄️ **Migrations**:
  - Development: Aplicadas automaticamente na inicialização
  - Staging/Production: Aplicar manualmente (conforme CLAUDE.md)
  - Nunca rodar migrations automáticas em produção
- 🗄️ **UUID v7 para IDs**: Todos os IDs usam `Guid.CreateVersion7()` para performance
- 🗄️ **Tabela Outbox**: `OutboxMessages` no `ordersdb` para Transactional Outbox Pattern
- 🗄️ Considerar usar SQL Server gerenciado para produção (Azure SQL, AWS RDS)
- 🗄️ Planejar estratégia de backup e restore desde o início

### Limites do Dokploy e Adaptações
- ⚙️ **Aspire sem AppHost**: Em produção, não usamos AppHost (apenas desenvolvimento)
- ⚙️ **Service Discovery Manual**: Configurar via variáveis de ambiente:
  - `services__catalog-api__http__0=http://claudicando-catalog-api:8080`
  - ServiceDefaults resolve automaticamente com essas variáveis
- ⚙️ **Aspire Dashboard**: Não deployar em produção (apenas development)
- ⚙️ **OpenTelemetry**: Configurar endpoint externo se quiser observabilidade em produção:
  - `OTEL_EXPORTER_OTLP_ENDPOINT=http://<grafana>:4317`
- ⚙️ **Escalabilidade Horizontal**: Dokploy pode ter limitações
  - Para alta escala, considerar migração para Kubernetes
  - Replicar Catalog.Api e Orders.Api se necessário
- ⚙️ **Background Services**: OutboxProcessor roda dentro do Orders.Api (não precisa de container separado)

### Performance
- 🚀 **Imagens Docker**: .NET 10 runtime otimizado (multi-stage builds)
- 🚀 **Cache Redis**: Já implementado no Catalog.Api (TTL 5 minutos)
  - Cache de listagem de produtos
  - Invalidação ao criar/atualizar/deletar
- 🚀 **Connection Pooling**: Configurado automaticamente pelo EF Core
- 🚀 **UUID v7**: Melhor performance de índices B-tree no SQL Server
- 🚀 **Transactional Outbox**: Garante consistência eventual sem impactar performance de escrita
- 🚀 **OutboxProcessor**: Batch de 10 mensagens a cada 5s (ajustável conforme carga)
- 🚀 **Blazor Server**: Considerar Blazor WebAssembly para reduzir carga no servidor (futuro)
- 🚀 **CDN**: Considerar para assets estáticos do Admin Dashboard (futuro)

### Custos
- 💰 **Recursos Mínimos Estimados** (Dokploy):
  - **Catalog.Api**: 0.5 CPU, 512MB RAM
  - **Orders.Api**: 0.5 CPU, 512MB RAM (inclui OutboxProcessor)
  - **Admin Dashboard**: 0.5 CPU, 512MB RAM
  - **SQL Server**: 1 CPU, 2GB RAM (mínimo)
  - **Redis**: 0.25 CPU, 256MB RAM
  - **RabbitMQ**: 0.5 CPU, 512MB RAM
  - **Total**: ~3 CPU, 4.5GB RAM (considerar buffer de 50%)
- 💰 **GHCR**: Gratuito para imagens públicas, limites para privadas
- 💰 **Bandwidth**: Considerar custos de tráfego (dependendo do servidor)
- 💰 **SQL Server Gerenciado**: Avaliar custo-benefício (Azure SQL, AWS RDS) vs self-hosted
- 💰 **Escalabilidade**: Custos aumentam linearmente com replicação de serviços

---

## Próximos Passos

Após completar todas as fases deste plano:

1. **Monitorar** a aplicação em produção por pelo menos 1 semana
2. **Documentar** quaisquer problemas encontrados e soluções
3. **Otimizar** configurações baseado em dados reais de uso
4. **Planejar** melhorias futuras (CI/CD avançado, testes automatizados, etc.)
5. **Revisar** segurança e compliance regularmente

---

## Recursos Úteis

- [Documentação .NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- [GitHub Container Registry Docs](https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Dokploy Documentation](https://dokploy.com/docs)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [ASP.NET Core Deployment](https://learn.microsoft.com/aspnet/core/host-and-deploy/)

---

**Versão do Documento:** 2.0
**Data:** Janeiro 2025
**Projeto:** claudicando - Sistema de Microserviços com .NET Aspire
**Autor:** Plano atualizado com especificações da solução real

---

## 📝 Changelog

### v2.0 (Janeiro 2025)
- ✅ Atualizado com especificações reais da solução claudicando
- ✅ Adicionado resumo executivo com serviços, infraestrutura e dependências
- ✅ Especificado 3 serviços deployáveis: Catalog.Api, Orders.Api, Admin Dashboard
- ✅ Detalhado infraestrutura: SQL Server (2 DBs), Redis, RabbitMQ
- ✅ Adicionado padrão Transactional Outbox Pattern e OutboxProcessor
- ✅ Especificado nomenclatura de imagens Docker no GHCR:
  - `claudicando-catalog-api`
  - `claudicando-orders-api`
  - `claudicando-admin-dashboard`
- ✅ Especificado domínios .net.br:
  - `catalog-api.claudicando.net.br`
  - `orders-api.claudicando.net.br`
  - `dashboard.claudicando.net.br`
- ✅ Detalhado variáveis de ambiente para cada serviço
- ✅ Adicionado fluxos de comunicação entre serviços (Service Discovery)
- ✅ Especificado endpoints públicos e exposição de portas
- ✅ Adicionado testes de integração específicos da solução
- ✅ Atualizado considerações de segurança, networking e performance
- ✅ Adicionado estimativas de recursos e custos

### v1.0 (Novembro 2025)
- 🔹 Plano genérico inicial para deploy de .NET Aspire no Dokploy
