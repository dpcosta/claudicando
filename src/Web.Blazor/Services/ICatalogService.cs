using Web.Blazor.Models;

namespace Web.Blazor.Services;

public interface ICatalogService
{
    Task<IEnumerable<ProductDto>?> GetAllProductsAsync();
    Task<ProductDto?> GetProductByIdAsync(Guid id);
}
