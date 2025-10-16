using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Data.Repositories;
using Orders.Api.DTOs;
using Orders.Api.Entities;
using Orders.Api.Services;
using Orders.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

// Add ServiceDefaults (Aspire)
builder.AddServiceDefaults();

// Add DbContext
builder.AddSqlServerDbContext<OrdersDbContext>("ordersdb");

// Add repositories
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Add HttpClient for Catalog.Api (Service Discovery)
builder.Services.AddHttpClient<ICatalogService, CatalogService>(client =>
{
    client.BaseAddress = new Uri("http://catalog-api");
});

// Add validators
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderValidator>();

// Add RabbitMQ Publisher (Singleton para gerenciar conexão)
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// Add Outbox Processor (Background Service)
builder.Services.AddHostedService<OutboxProcessor>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Orders API", Version = "v1" });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrdersDbContext>("database")
    .AddRabbitMQ(name: "rabbitmq");

var app = builder.Build();

// Apply migrations in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure middleware
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();

// Endpoints

// POST /api/orders - Criar pedido com padrão Outbox
app.MapPost("/api/orders", async (
    CreateOrderDto createOrderDto,
    IOrderRepository orderRepository,
    ICatalogService catalogService,
    IValidator<CreateOrderDto> validator,
    OrdersDbContext dbContext,
    ILogger<Program> logger) =>
{
    // Validar DTO
    var validationResult = await validator.ValidateAsync(createOrderDto);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    // Validar produtos no Catalog.Api
    var orderItems = new List<OrderItem>();
    decimal totalAmount = 0;

    foreach (var item in createOrderDto.Items)
    {
        var product = await catalogService.GetProductByIdAsync(item.ProductId);

        if (product == null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Produto não encontrado",
                detail: $"O produto {item.ProductId} não foi encontrado no catálogo");
        }

        if (product.Stock < item.Quantity)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Estoque insuficiente",
                detail: $"O produto {product.Name} possui apenas {product.Stock} unidades em estoque, mas foram solicitadas {item.Quantity}");
        }

        var orderItem = new OrderItem
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = item.Quantity,
            Price = product.Price
        };

        orderItems.Add(orderItem);
        totalAmount += orderItem.Price * orderItem.Quantity;
    }

    // Usar ExecutionStrategy para compatibilidade com retry do Aspire
    var strategy = dbContext.Database.CreateExecutionStrategy();

    return await strategy.ExecuteAsync(async () =>
    {
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // Criar pedido
            var order = new Order
            {
                Id = Guid.CreateVersion7(),
                UserId = createOrderDto.UserId,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                Items = orderItems
            };

            dbContext.Orders.Add(order);

            // Criar mensagem na Outbox (padrão Transactional Outbox)
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                EventType = "OrderCreated",
                Payload = JsonSerializer.Serialize(new
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    TotalAmount = order.TotalAmount,
                    CreatedAt = order.CreatedAt,
                    Items = order.Items.Select(i => new
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        Price = i.Price
                    })
                }),
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false
            };

            dbContext.OutboxMessages.Add(outboxMessage);

            // Commit da transação (garante atomicidade)
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logger.LogInformation("Order {OrderId} created for user {UserId} with {ItemCount} items. Total: {TotalAmount}. Outbox message saved.",
                order.Id, order.UserId, order.Items.Count, order.TotalAmount);

            var orderDto = new OrderDto(
                order.Id,
                order.UserId,
                order.TotalAmount,
                order.CreatedAt,
                order.Status,
                order.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductName, i.Quantity, i.Price)).ToList()
            );

            return Results.Created($"/api/orders/{order.Id}", orderDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Error creating order for user {UserId}", createOrderDto.UserId);
            throw;
        }
    });
})
.WithName("CreateOrder");

// GET /api/orders/{id} - Consultar pedido
app.MapGet("/api/orders/{id:guid}", async (Guid id, IOrderRepository orderRepository, ILogger<Program> logger) =>
{
    var order = await orderRepository.GetByIdAsync(id);

    if (order == null)
    {
        logger.LogWarning("Order {OrderId} not found", id);
        return Results.NotFound(new { message = "Pedido não encontrado" });
    }

    var orderDto = new OrderDto(
        order.Id,
        order.UserId,
        order.TotalAmount,
        order.CreatedAt,
        order.Status,
        order.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductName, i.Quantity, i.Price)).ToList()
    );

    return Results.Ok(orderDto);
})
.WithName("GetOrderById");

// GET /api/orders - Lista pedidos (query: userId)
app.MapGet("/api/orders", async (IOrderRepository orderRepository, string? userId, ILogger<Program> logger) =>
{
    IEnumerable<Order> orders;

    if (!string.IsNullOrEmpty(userId))
    {
        logger.LogInformation("Fetching orders for user {UserId}", userId);
        orders = await orderRepository.GetByUserIdAsync(userId);
    }
    else
    {
        logger.LogInformation("Fetching all orders");
        orders = await orderRepository.GetAllAsync();
    }

    var orderDtos = orders.Select(o => new OrderDto(
        o.Id,
        o.UserId,
        o.TotalAmount,
        o.CreatedAt,
        o.Status,
        o.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductName, i.Quantity, i.Price)).ToList()
    ));

    return Results.Ok(orderDtos);
})
.WithName("GetOrders");

app.Run();
