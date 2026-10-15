using System.Collections.Concurrent;
using Commerce.Application.Products;
using Commerce.Domain.Entities;

namespace Commerce.Infrastructure.Repositories;

public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();

    public InMemoryProductRepository()
    {
        Product[] seed =
        [
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Mechanical Keyboard",
                "Compact wireless keyboard for your workspace.",
                89.99m,
                "Accessories",
                20),
            new(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Wireless Headphones",
                "Comfortable headphones with noise cancellation.",
                149.99m,
                "Audio",
                15),
            new(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "USB-C Dock",
                "Connect your display, network, and accessories.",
                119.00m,
                "Accessories",
                10)
        ];

        foreach (var product in seed)
        {
            _products.TryAdd(product.Id, product);
        }
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Product> products = _products.Values
            .OrderBy(product => product.Name)
            .ToArray();

        return Task.FromResult(products);
    }

    public Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _products.TryGetValue(id, out var product);

        return Task.FromResult(product);
    }

    public Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_products.TryAdd(product.Id, product))
        {
            throw new InvalidOperationException(
                "A product with this ID already exists.");
        }

        return Task.CompletedTask;
    }
}
