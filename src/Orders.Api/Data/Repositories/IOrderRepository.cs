using Orders.Api.Entities;

namespace Orders.Api.Data.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task<IEnumerable<Order>> GetAllAsync();
    Task<IEnumerable<Order>> GetByUserIdAsync(string userId);
    Task<Order> CreateAsync(Order order);
}
