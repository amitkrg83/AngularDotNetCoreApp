namespace Commerce.Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    int StockQuantity);
