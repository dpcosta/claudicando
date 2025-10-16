using System.Net.Http.Json;
using Web.Blazor.Models;

namespace Web.Blazor.Services;

public class CatalogService : ICatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductDto>?> GetAllProductsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching all products from Catalog API");

            var products = await _httpClient.GetFromJsonAsync<IEnumerable<ProductDto>>("api/products");

            _logger.LogInformation("Successfully fetched {Count} products", products?.Count() ?? 0);

            return products;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching products from Catalog API");
            return null;
        }
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Fetching product {ProductId} from Catalog API", id);

            var product = await _httpClient.GetFromJsonAsync<ProductDto>($"api/products/{id}");

            if (product != null)
            {
                _logger.LogInformation("Successfully fetched product {ProductId}", id);
            }
            else
            {
                _logger.LogWarning("Product {ProductId} not found", id);
            }

            return product;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error fetching product {ProductId} from Catalog API", id);
            return null;
        }
    }
}
