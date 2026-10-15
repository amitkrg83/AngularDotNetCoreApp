namespace Commerce.Application.Products;

public sealed class ProductService(
    IProductRepository repository) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await repository.GetAllAsync(cancellationToken);

        return products
            .Select(product => new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.Category,
                product.StockQuantity))
            .ToArray();
    }
}
