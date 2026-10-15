using System.Collections.Concurrent;
using System.Security.Cryptography;
using Commerce.Application;
using Commerce.Domain;
namespace Commerce.Infrastructure;

public sealed class MutationLock : IMutationLock
{
    private readonly SemaphoreSlim gate=new(1,1);
    public async Task<IDisposable> EnterAsync(CancellationToken ct=default) { await gate.WaitAsync(ct);return new Lease(gate); }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable { public void Dispose()=>gate.Release(); }
}
public sealed class PasswordService : IPasswordService
{
    public string Hash(string password)
    {
        var salt=RandomNumberGenerator.GetBytes(16);
        var key=Rfc2898DeriveBytes.Pbkdf2(password,salt,210000,HashAlgorithmName.SHA256,32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }
    public bool Verify(string password,string hash)
    {
        var parts=hash.Split('.');
        var actual=Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(parts[0]),210000,HashAlgorithmName.SHA256,32);
        return CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(parts[1]));
    }
}
public sealed class MemoryProducts : IProductRepository
{
    private readonly ConcurrentDictionary<Guid,Product> data=new();
    public MemoryProducts()
    {
        var names=new[]{("Mechanical Keyboard","Accessories",89.99m,"photo-1587829741301-dc798b83add3"),
            ("Studio Headphones","Audio",149.99m,"photo-1546435770-a3e426bf472b"),
            ("Everyday Backpack","Lifestyle",64.00m,"photo-1553062407-98eeb64c6a62"),
            ("Desk Lamp","Workspace",49.00m,"photo-1507473885765-e6ed057f782c"),
            ("Wireless Mouse","Accessories",39.99m,"photo-1527814050087-3793815479db"),
            ("Travel Camera","Lifestyle",399.00m,"photo-1516035069371-29a1b244cc32")};
        for(var i=0;i<names.Length;i++) { var (name,category,price,img)=names[i];var id=Guid.Parse($"00000000-0000-0000-0000-{i+1:000000000000}");Save(new(id,name,$"A thoughtfully selected {name.ToLowerInvariant()} for your everyday routine.",price,category,12,$"https://images.unsplash.com/{img}?auto=format&fit=crop&w=640&q=80")); }
    }
    public IReadOnlyList<Product> All()=>data.Values.ToArray();
    public Product? Get(Guid id)=>data.GetValueOrDefault(id);
    public void Save(Product product)=>data[product.Id]=product;
}
public sealed class MemoryUsers : IUserRepository
{
    private readonly ConcurrentDictionary<Guid,User> data=new();
    public MemoryUsers(IPasswordService passwords)
    {
        Save(new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),"Demo Admin","admin@commerce.test",passwords.Hash("AdminDemo123!"),"Admin"));
        Save(new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),"Demo Customer","customer@commerce.test",passwords.Hash("CustomerDemo123!"),"Customer"));
    }
    public IReadOnlyList<User> All()=>data.Values.ToArray();
    public User? Get(Guid id)=>data.GetValueOrDefault(id);
    public User? FindByEmail(string email)=>data.Values.SingleOrDefault(u=>u.Email.Equals(email,StringComparison.OrdinalIgnoreCase));
    public void Save(User user)=>data[user.Id]=user;
}
public sealed class MemoryOrders : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid,Order> data=new();
    public IReadOnlyList<Order> All()=>data.Values.ToArray();
    public Order? Get(Guid id)=>data.GetValueOrDefault(id);
    public Order? FindByKey(Guid userId,string key)=>data.Values.SingleOrDefault(o=>o.UserId==userId && o.RequestKey==key);
    public void Save(Order order)=>data[order.Id]=order;
}
