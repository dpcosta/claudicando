namespace Orders.Api.Services;

public interface ICatalogService
{
    Task<ProductDto?> GetProductByIdAsync(Guid productId);
}

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock
);
