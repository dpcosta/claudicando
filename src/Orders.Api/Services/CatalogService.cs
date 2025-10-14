using System.Text.Json;

namespace Orders.Api.Services;

public class CatalogService : ICatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid productId)
    {
        try
        {
            _logger.LogInformation("Fetching product {ProductId} from Catalog.Api", productId);

            var response = await _httpClient.GetAsync($"/api/products/{productId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Product {ProductId} not found in Catalog.Api. Status: {StatusCode}",
                    productId, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductDto>(content, _jsonOptions);

            _logger.LogInformation("Product {ProductId} fetched successfully", productId);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product {ProductId} from Catalog.Api", productId);
            throw;
        }
    }
}
