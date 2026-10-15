namespace Commerce.Application.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
