using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Commerce.Domain;
namespace Commerce.Application;

// Coordinates use cases. Controllers contain no inventory or payment business rules.
public sealed class CommerceService(IProductRepository products, IUserRepository users,
    IOrderRepository orders, IMutationLock mutations, IPasswordService passwords,
    IPaymentGateway gateway)
{
    public static PublicUser Public(User user) => new(user.Id,user.Name,user.Email,user.Role,user.Active);
    public IReadOnlyList<Product> Products(bool admin = false) => products.All().Where(p => admin || p.Active).OrderBy(p => p.Name).ToArray();
    public IReadOnlyList<PublicUser> Users() => users.All().Select(Public).ToArray();
    public IReadOnlyList<Order> Orders(Guid userId, bool admin) => orders.All().Where(o => admin || o.UserId == userId).OrderByDescending(o => o.CreatedAt).ToArray();
    public User? User(Guid id) => users.Get(id);
    public Product Product(Guid id) => products.Get(id) is { Active: true } p ? p : throw new ShopException(404,"Product not found.");

    public async Task<User> Register(string name,string email,string password)
    {
        name=name.Trim();email=email.Trim().ToLowerInvariant();
        if(name.Length is <2 or >80 || email.Length>200 || !System.Net.Mail.MailAddress.TryCreate(email,out _) || password.Length is <10 or >128)
            throw new ShopException(400,"Use a valid name/email and a password of 10–128 characters.");
        using var lease=await mutations.EnterAsync();
        if(users.FindByEmail(email)!=null) throw new ShopException(409,"Email is already registered.");
        var user=new User(Guid.NewGuid(),name,email,passwords.Hash(password),"Customer");
        users.Save(user);return user;
    }
    public User Login(string email,string password)
    {
        var user=users.FindByEmail(email.Trim());
        // Perform expensive verification even when no account exists.
        var valid=passwords.Verify(password,user?.PasswordHash ?? DummyHash);
        if(user is null || !valid || !user.Active) throw new ShopException(401,"Invalid email or password.");
        return user;
    }
    private readonly string DummyHash = passwords.Hash("Non-account-placeholder-123!");

    public async Task<Product> SaveProduct(Guid? id,ProductInput input)
    {
        if(string.IsNullOrWhiteSpace(input.Name) || input.Name.Length>100 || string.IsNullOrWhiteSpace(input.Description) || input.Description.Length>2000 ||
           string.IsNullOrWhiteSpace(input.Category) || input.Category.Length>50 || input.Price<=0 || input.Price>100000 || decimal.Round(input.Price,2)!=input.Price || input.StockQuantity is <0 or >100000 ||
           input.ImageUrl.Length>2048 || (input.ImageUrl.Length>0 && (!Uri.TryCreate(input.ImageUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https")))
            throw new ShopException(400,"Check product fields. Price must be positive with at most 2 decimals; image must use HTTPS.");
        using var lease=await mutations.EnterAsync();
        if(id.HasValue && products.Get(id.Value) is null) throw new ShopException(404,"Product not found.");
        var product=new Product(id ?? Guid.NewGuid(),input.Name.Trim(),input.Description.Trim(),input.Price,input.Category.Trim(),input.StockQuantity,input.ImageUrl,input.Active);
        products.Save(product);return product;
    }
    public async Task DeactivateProduct(Guid id)
    {
        using var lease=await mutations.EnterAsync();
        var product=products.Get(id) ?? throw new ShopException(404,"Product not found.");
        products.Save(product with { Active=false });
    }
    public async Task UpdateUser(Guid actor,Guid id,string role,bool active)
    {
        if(role is not ("Admin" or "Customer")) throw new ShopException(400,"Invalid role.");
        using var lease=await mutations.EnterAsync();
        var user=users.Get(id) ?? throw new ShopException(404,"User not found.");
        if(id==actor && (!active || role!="Admin")) throw new ShopException(400,"You cannot remove your own admin access.");
        users.Save(user with { Role=role,Active=active,Version=user.Version+1 });
    }
    public async Task<CheckoutResult> Checkout(Guid userId,string key,CheckoutInput input,CancellationToken ct)
    {
        if(!Guid.TryParse(key,out _) || input.Items is not { Length: >0 and <=50 } || input.Items.Any(i=>i.Quantity is <1 or >99) || input.Items.Select(i=>i.ProductId).Distinct().Count()!=input.Items.Length)
            throw new ShopException(400,"Use a UUID checkout key, 1–50 unique products, and quantities of 1–99.");
        var address=input.Shipping;
        if(address is null || new[]{address.Name,address.Line1,address.City,address.PostalCode,address.Country}.Any(s=>string.IsNullOrWhiteSpace(s) || s.Length>200) || address.Country!="US")
            throw new ShopException(400,"Complete the shipping address. This demo supports US addresses only.");
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Items=input.Items.OrderBy(i=>i.ProductId),input.Shipping }))));
        using var lease=await mutations.EnterAsync(ct);
        var existing=orders.FindByKey(userId,key);
        Order order;
        if(existing is not null)
        {
            if(existing.Fingerprint!=fingerprint) throw new ShopException(409,"Checkout key was already used with a different cart/address.");
            if(existing.PaymentStatus!="Pending") throw new ShopException(409,"Checkout already finished. View your order or start a new checkout.");
            if(existing.CheckoutUrl is not null) return new(existing.Id,existing.CheckoutUrl,gateway.Mode);
            order=existing;
        }
        else
        {
            var lines=input.Items.Select(item=>{
                var p=Product(item.ProductId);
                if(item.Quantity>p.StockQuantity) throw new ShopException(409,$"Insufficient stock for {p.Name}.");
                return new OrderLine(p.Id,p.Name,p.Price,item.Quantity);
            }).ToArray();
            order=new(Guid.NewGuid(),userId,lines,address,lines.Sum(l=>l.UnitPrice*l.Quantity),"Pending","Unfulfilled",DateTimeOffset.UtcNow,key,fingerprint);
            // Reserve stock. Failure/expiration events return it exactly once.
            foreach(var line in lines) { var p=products.Get(line.ProductId)!;products.Save(p with { StockQuantity=p.StockQuantity-line.Quantity }); }
            orders.Save(order);
        }
        // Retry with the same order + Stripe idempotency key after an ambiguous timeout.
        // Never release stock on a network timeout: the external session may exist.
        var user=users.Get(userId) ?? throw new ShopException(401,"Account unavailable.");
        var session=await gateway.CreateAsync(order,user.Email,ct);
        orders.Save(order with { SessionId=session.Id,CheckoutUrl=session.Url });
        return new(order.Id,session.Url,gateway.Mode);
    }
    public async Task PaymentEvent(GatewayEvent evt)
    {
        using var lease=await mutations.EnterAsync();
        var order=orders.All().SingleOrDefault(o=>o.SessionId==evt.SessionId);
        // Non-2xx allows Stripe to retry a callback arriving before session persistence.
        if(order is null) throw new ShopException(409,"Payment session not recorded yet; retry event.");
        if(evt.Currency!="usd" || evt.AmountCents!=(long)(order.Total*100)) throw new ShopException(400,"Payment amount/currency mismatch.");
        ApplyPayment(order,evt.Status);
    }
    public async Task<Order> Simulate(Guid userId,Guid orderId,string outcome)
    {
        if(gateway.Mode!="Simulated") throw new ShopException(404,"Simulator is disabled.");
        if(outcome is not ("Paid" or "Failed" or "Canceled")) throw new ShopException(400,"Invalid outcome.");
        using var lease=await mutations.EnterAsync();
        var order=orders.Get(orderId) ?? throw new ShopException(404,"Order not found.");
        if(order.UserId!=userId) throw new ShopException(404,"Order not found.");
        return ApplyPayment(order,outcome);
    }
    private Order ApplyPayment(Order order,string status)
    {
        if(order.PaymentStatus!="Pending") return order; // Idempotent terminal transition.
        if(status is not ("Paid" or "Failed" or "Canceled" or "Expired")) throw new ShopException(400,"Invalid payment status.");
        if(status!="Paid") foreach(var line in order.Lines) { var p=products.Get(line.ProductId)!;products.Save(p with { StockQuantity=p.StockQuantity+line.Quantity }); }
        var updated=order with { PaymentStatus=status };orders.Save(updated);return updated;
    }
    public async Task UpdateFulfillment(Guid id,string status)
    {
        using var lease=await mutations.EnterAsync();
        var order=orders.Get(id) ?? throw new ShopException(404,"Order not found.");
        if(order.PaymentStatus!="Paid") throw new ShopException(409,"Only paid orders can be fulfilled.");
        if(!((order.FulfillmentStatus=="Unfulfilled" && status=="Processing") || (order.FulfillmentStatus=="Processing" && status=="Shipped") || (order.FulfillmentStatus=="Shipped" && status=="Delivered")))
            throw new ShopException(409,"Move forward one step: Processing → Shipped → Delivered.");
        orders.Save(order with { FulfillmentStatus=status });
    }
}
