namespace Commerce.Domain.Entities;

public sealed record Product(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    int StockQuantity);
