namespace Commerce.Domain;

public sealed record Product(Guid Id, string Name, string Description, decimal Price,
    string Category, int StockQuantity, string ImageUrl, bool Active = true);
public sealed record User(Guid Id, string Name, string Email, string PasswordHash,
    string Role, bool Active = true, int Version = 1);
public sealed record ShippingAddress(string Name, string Line1, string City, string PostalCode, string Country);
public sealed record OrderLine(Guid ProductId, string Name, decimal UnitPrice, int Quantity);
public sealed record Order(Guid Id, Guid UserId, OrderLine[] Lines, ShippingAddress Shipping,
    decimal Total, string PaymentStatus, string FulfillmentStatus, DateTimeOffset CreatedAt,
    string RequestKey, string Fingerprint, string? SessionId = null, string? CheckoutUrl = null);
