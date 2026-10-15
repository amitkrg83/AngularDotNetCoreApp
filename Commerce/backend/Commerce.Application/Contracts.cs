using Commerce.Domain;
namespace Commerce.Application;

public interface IProductRepository
{
    IReadOnlyList<Product> All();
    Product? Get(Guid id);
    void Save(Product product);
}
public interface IUserRepository
{
    IReadOnlyList<User> All();
    User? Get(Guid id);
    User? FindByEmail(string email);
    void Save(User user);
}
public interface IOrderRepository
{
    IReadOnlyList<Order> All();
    Order? Get(Guid id);
    Order? FindByKey(Guid userId, string key);
    void Save(Order order);
}
// Serializes mutations for a single-process in-memory demo. A database adapter
// must replace this with transactional persistence and concurrency controls.
public interface IMutationLock
{
    Task<IDisposable> EnterAsync(CancellationToken ct = default);
}
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
public record PaymentSession(string Id, string Url);
public record GatewayEvent(string SessionId, string Status, long AmountCents, string Currency);
public interface IPaymentGateway
{
    string Mode { get; }
    Task<PaymentSession> CreateAsync(Order order, string email, CancellationToken ct);
    GatewayEvent? VerifyEvent(string body, string signature);
}
public sealed class ShopException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed record PublicUser(Guid Id, string Name, string Email, string Role, bool Active);
public sealed record CartLine(Guid ProductId, int Quantity);
public sealed record CheckoutInput(CartLine[] Items, ShippingAddress Shipping);
public sealed record CheckoutResult(Guid OrderId, string Url, string Mode);
public sealed record ProductInput(string Name, string Description, decimal Price,
    string Category, int StockQuantity, string ImageUrl, bool Active = true);
