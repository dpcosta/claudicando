using Orders.Api.Entities;

namespace Orders.Api.DTOs;

public record OrderDto(
    Guid Id,
    string UserId,
    decimal TotalAmount,
    DateTime CreatedAt,
    OrderStatus Status,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal Price
);

public record CreateOrderDto(
    string UserId,
    List<CreateOrderItemDto> Items
);

public record CreateOrderItemDto(
    Guid ProductId,
    int Quantity
);
