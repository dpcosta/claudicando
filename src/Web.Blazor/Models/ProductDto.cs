namespace Web.Blazor.Models;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
