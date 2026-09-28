using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Application.Catalog;
using Product.Domain.Catalog;

namespace Product.Infrastructure.Persistence;

public sealed class CatalogRepository(ProductDbContext context) : ICatalogRepository
{
    public Task<Category?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken) =>
        context.Categories.SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken);

    public Task<Category?> GetCategoryBySlugAsync(string slug, CancellationToken cancellationToken) =>
        context.Categories.SingleOrDefaultAsync(category => category.Slug == slug, cancellationToken);

    public async Task AddCategoryAsync(Category category, CancellationToken cancellationToken) =>
        await context.Categories.AddAsync(category, cancellationToken);

    public async Task<PagedList<CategorySummary>> ListCategoriesAsync(
        int page,
        int pageSize,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = context.Categories.AsNoTracking();
        if (activeOnly)
            query = query.Where(category => category.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(category => new CategorySummary(category.Id, category.Name, category.Slug, category.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedList<CategorySummary>(items, page, pageSize, total);
    }

    public Task<ProductItem?> GetProductForUpdateAsync(Guid productId, CancellationToken cancellationToken) =>
        context.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

    public Task<ProductItem?> GetProductBySkuAsync(string normalizedSku, CancellationToken cancellationToken) =>
        context.Products.SingleOrDefaultAsync(product => product.Sku == normalizedSku, cancellationToken);

    public async Task<ProductDetails?> GetProductDetailsAsync(Guid productId, CancellationToken cancellationToken)
    {
        // One join. The category name is projected here so the API does not query categories in a loop.
        var row = await context.Products.AsNoTracking()
            .Where(product => product.Id == productId)
            .Join(
                context.Categories.AsNoTracking(),
                product => product.CategoryId,
                category => category.Id,
                (product, category) => new
                {
                    product.Id,
                    product.Sku,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.Currency,
                    product.CategoryId,
                    CategoryName = category.Name,
                    product.IsActive,
                    product.RowVersion,
                    product.UpdatedAtUtc
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        return new ProductDetails(
            row.Id,
            row.Sku,
            row.Name,
            row.Description,
            row.Price,
            row.Currency,
            row.CategoryId,
            row.CategoryName,
            row.IsActive,
            RowVersionToken.Encode(row.RowVersion),
            row.UpdatedAtUtc);
    }

    public async Task AddProductAsync(ProductItem product, CancellationToken cancellationToken) =>
        await context.Products.AddAsync(product, cancellationToken);

    public async Task<PagedList<ProductSummary>> SearchProductsAsync(ProductSearch search, CancellationToken cancellationToken)
    {
        var products = context.Products.AsNoTracking().AsQueryable();
        if (search.ActiveOnly)
            products = products.Where(product => product.IsActive);
        if (search.CategoryId is Guid categoryId)
            products = products.Where(product => product.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            // Contains becomes LIKE '%term%'. A leading wildcard will not seek IX_Products_IsActive_Name_Id.
            products = products.Where(product => product.Name.Contains(term) || product.Sku.Contains(term));
        }

        products = search.Sort switch
        {
            ProductSort.PriceAscending => products.OrderBy(product => product.Price).ThenBy(product => product.Id),
            ProductSort.PriceDescending => products.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            ProductSort.Newest => products.OrderByDescending(product => product.CreatedAtUtc).ThenBy(product => product.Id),
            _ => products.OrderBy(product => product.Name).ThenBy(product => product.Id)
        };

        var total = await products.CountAsync(cancellationToken);
        var items = await products
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Join(
                context.Categories.AsNoTracking(),
                product => product.CategoryId,
                category => category.Id,
                (product, category) => new ProductSummary(
                    product.Id,
                    product.Sku,
                    product.Name,
                    product.Price,
                    product.Currency,
                    product.CategoryId,
                    category.Name,
                    product.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedList<ProductSummary>(items, search.Page, search.PageSize, total);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ProductConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Products_Sku"))
        {
            throw new SkuAlreadyExistsException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Categories_Slug"))
        {
            throw new SlugAlreadyExistsException(exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
}
