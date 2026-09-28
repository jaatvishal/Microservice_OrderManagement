using Product.Application.Catalog;
using Product.Domain.Catalog;

namespace Product.Application.Abstractions;

public interface ICatalogRepository
{
    Task<Category?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<Category?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken);

    Task AddCategoryAsync(Category category, CancellationToken cancellationToken);

    Task<PagedList<CategorySummary>> ListCategoriesAsync(int page, int pageSize, bool activeOnly, CancellationToken cancellationToken);

    Task<ProductItem?> GetProductForUpdateAsync(Guid productId, CancellationToken cancellationToken);

    Task<ProductItem?> GetProductBySkuAsync(string normalizedSku, CancellationToken cancellationToken);

    Task<ProductDetails?> GetProductDetailsAsync(Guid productId, CancellationToken cancellationToken);

    Task AddProductAsync(ProductItem product, CancellationToken cancellationToken);

    Task<PagedList<ProductSummary>> SearchProductsAsync(ProductSearch search, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
