namespace Catalog.Api.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateProductDto(
    string Name,
    string Description,
    decimal Price,
    int Stock
);

public record UpdateProductDto(
    string Name,
    string Description,
    decimal Price,
    int Stock
);
