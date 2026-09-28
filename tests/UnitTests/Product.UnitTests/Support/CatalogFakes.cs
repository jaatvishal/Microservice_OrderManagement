using BuildingBlocks.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Product.Application.Abstractions;
using Product.Application.Catalog;
using Product.Domain.Catalog;

namespace Product.UnitTests.Support;

internal sealed class FixedClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly List<Category> _categories = [];
    private readonly List<ProductItem> _products = [];
    private int _version;

    public Exception? SaveException { get; set; }

    public Task<Category?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(_categories.SingleOrDefault(category => category.Id == categoryId));

    public Task<Category?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(_categories.SingleOrDefault(category => category.Slug == slug));

    public Task AddCategoryAsync(Category category, CancellationToken cancellationToken)
    {
        _categories.Add(category);
        return Task.CompletedTask;
    }

    public Task<PagedList<CategorySummary>> ListCategoriesAsync(int page, int pageSize, bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _categories.AsEnumerable();
        if (activeOnly)
            query = query.Where(category => category.IsActive);

        var all = query.OrderBy(category => category.Name).ThenBy(category => category.Id).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(category => new CategorySummary(category.Id, category.Name, category.Slug, category.IsActive))
            .ToList();
        return Task.FromResult(new PagedList<CategorySummary>(items, page, pageSize, all.Count));
    }

    public Task<ProductItem?> GetProductForUpdateAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_products.SingleOrDefault(product => product.Id == productId));

    public Task<ProductItem?> GetProductBySkuAsync(string normalizedSku, CancellationToken cancellationToken) =>
        Task.FromResult(_products.SingleOrDefault(product => product.Sku == normalizedSku));

    public Task<ProductDetails?> GetProductDetailsAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = _products.SingleOrDefault(candidate => candidate.Id == productId);
        if (product is null)
            return Task.FromResult<ProductDetails?>(null);

        var category = _categories.Single(candidate => candidate.Id == product.CategoryId);
        return Task.FromResult<ProductDetails?>(CreateProductHandler.ToDetails(product, category));
    }

    public Task AddProductAsync(ProductItem product, CancellationToken cancellationToken)
    {
        _products.Add(product);
        return Task.CompletedTask;
    }

    public Task<PagedList<ProductSummary>> SearchProductsAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        IEnumerable<ProductItem> query = _products;
        if (search.ActiveOnly)
            query = query.Where(product => product.IsActive);
        if (search.CategoryId is Guid categoryId)
            query = query.Where(product => product.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(product =>
                product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || product.Sku.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = search.Sort switch
        {
            ProductSort.PriceAscending => query.OrderBy(product => product.Price).ThenBy(product => product.Id),
            ProductSort.PriceDescending => query.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            ProductSort.Newest => query.OrderByDescending(product => product.CreatedAtUtc).ThenBy(product => product.Id),
            _ => query.OrderBy(product => product.Name).ThenBy(product => product.Id)
        };

        var all = query.ToList();
        var items = all.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .Select(product =>
            {
                var category = _categories.Single(category => category.Id == product.CategoryId);
                return new ProductSummary(product.Id, product.Sku, product.Name, product.Price, product.Currency, product.CategoryId, category.Name, product.IsActive);
            })
            .ToList();

        return Task.FromResult(new PagedList<ProductSummary>(items, search.Page, search.PageSize, all.Count));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (SaveException is not null)
            throw SaveException;

        foreach (var product in _products)
        {
            _version++;
            var bytes = new byte[8];
            BitConverter.GetBytes(_version).CopyTo(bytes, 0);
            product.StampRowVersion(bytes);
        }

        return Task.CompletedTask;
    }
}

internal static class NullLog
{
    public static ILogger<T> For<T>() => NullLogger<T>.Instance;
}
