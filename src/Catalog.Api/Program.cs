using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.Data.Repositories;
using Catalog.Api.DTOs;
using Catalog.Api.Entities;
using Catalog.Api.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

// Add ServiceDefaults (deve ser o primeiro)
builder.AddServiceDefaults();

// Add DbContext
builder.AddSqlServerDbContext<CatalogDbContext>("catalogdb");

// Add Redis cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
});

// Add repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Add validators
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Catalog API", Version = "v1" });
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

var app = builder.Build();

// Aplicar migrations automaticamente APENAS EM DESENVOLVIMENTO
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }
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
var productsGroup = app.MapGroup("/api/products").WithTags("Products");

// GET /api/products - Lista todos os produtos
productsGroup.MapGet("/", async (IProductRepository repository, IDistributedCache cache) =>
{
    const string cacheKey = "products:all";

    // Tentar obter do cache
    var cachedProducts = await cache.GetStringAsync(cacheKey);
    if (!string.IsNullOrEmpty(cachedProducts))
    {
        var products = JsonSerializer.Deserialize<IEnumerable<ProductDto>>(cachedProducts);
        return Results.Ok(products);
    }

    // Buscar do banco
    var productsFromDb = await repository.GetAllAsync();
    var productDtos = productsFromDb.Select(p => new ProductDto(
        p.Id,
        p.Name,
        p.Description,
        p.Price,
        p.Stock,
        p.CreatedAt,
        p.UpdatedAt
    ));

    // Salvar no cache por 5 minutos
    var cacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(productDtos), cacheOptions);

    return Results.Ok(productDtos);
})
.WithName("GetAllProducts");

// GET /api/products/{id} - Busca produto por ID
productsGroup.MapGet("/{id:guid}", async (Guid id, IProductRepository repository) =>
{
    var product = await repository.GetByIdAsync(id);
    if (product == null)
    {
        return Results.NotFound(new { message = "Produto não encontrado" });
    }

    var productDto = new ProductDto(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.Stock,
        product.CreatedAt,
        product.UpdatedAt
    );

    return Results.Ok(productDto);
})
.WithName("GetProductById");

// POST /api/products - Cria novo produto
productsGroup.MapPost("/", async (
    CreateProductDto dto,
    IProductRepository repository,
    IValidator<CreateProductDto> validator,
    IDistributedCache cache) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    var product = new Product
    {
        Name = dto.Name,
        Description = dto.Description,
        Price = dto.Price,
        Stock = dto.Stock
    };

    var createdProduct = await repository.CreateAsync(product);

    // Invalidar cache
    await cache.RemoveAsync("products:all");

    var productDto = new ProductDto(
        createdProduct.Id,
        createdProduct.Name,
        createdProduct.Description,
        createdProduct.Price,
        createdProduct.Stock,
        createdProduct.CreatedAt,
        createdProduct.UpdatedAt
    );

    return Results.Created($"/api/products/{productDto.Id}", productDto);
})
.WithName("CreateProduct");

// PUT /api/products/{id} - Atualiza produto
productsGroup.MapPut("/{id:guid}", async (
    Guid id,
    UpdateProductDto dto,
    IProductRepository repository,
    IValidator<UpdateProductDto> validator,
    IDistributedCache cache) =>
{
    var validationResult = await validator.ValidateAsync(dto);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    var product = await repository.GetByIdAsync(id);
    if (product == null)
    {
        return Results.NotFound(new { message = "Produto não encontrado" });
    }

    product.Name = dto.Name;
    product.Description = dto.Description;
    product.Price = dto.Price;
    product.Stock = dto.Stock;

    await repository.UpdateAsync(product);

    // Invalidar cache
    await cache.RemoveAsync("products:all");

    var productDto = new ProductDto(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.Stock,
        product.CreatedAt,
        product.UpdatedAt
    );

    return Results.Ok(productDto);
})
.WithName("UpdateProduct");

// DELETE /api/products/{id} - Remove produto
productsGroup.MapDelete("/{id:guid}", async (Guid id, IProductRepository repository, IDistributedCache cache) =>
{
    var exists = await repository.ExistsAsync(id);
    if (!exists)
    {
        return Results.NotFound(new { message = "Produto não encontrado" });
    }

    await repository.DeleteAsync(id);

    // Invalidar cache
    await cache.RemoveAsync("products:all");

    return Results.NoContent();
})
.WithName("DeleteProduct");

app.Run();
