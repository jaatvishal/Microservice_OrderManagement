using BuildingBlocks.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using Product.Domain.Catalog;

namespace Product.Infrastructure.Persistence;

public static class ProductDatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        bool allowDevelopmentSeed,
        CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Database:ApplyMigrations", false))
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ProductDatabaseInitializer));
            logger.LogInformation("Applying Product database migrations.");

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
        }

        if (!allowDevelopmentSeed || !configuration.GetValue("Database:SeedSampleCatalog", false))
            return;

        await SeedSampleCatalogAsync(services, cancellationToken);
    }

    private static async Task SeedSampleCatalogAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ProductDatabaseInitializer));
        var now = clock.UtcNow;

        var books = await EnsureCategoryAsync(catalog, "Books", now, cancellationToken);
        var electronics = await EnsureCategoryAsync(catalog, "Electronics", now, cancellationToken);

        await EnsureProductAsync(catalog, "BOOK-001", "Distributed Systems Primer", 42.00m, books.Id, now, cancellationToken);
        await EnsureProductAsync(catalog, "ELEC-001", "Wireless Mouse", 25.50m, electronics.Id, now, cancellationToken);
        await catalog.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sample catalog seed completed.");
    }

    private static async Task<Category> EnsureCategoryAsync(
        ICatalogRepository catalog,
        string name,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var slug = CatalogSlug.FromName(name);
        var existing = await catalog.GetCategoryBySlugAsync(slug, cancellationToken);
        if (existing is not null)
            return existing;

        var category = Category.Create(name, utcNow);
        await catalog.AddCategoryAsync(category, cancellationToken);
        return category;
    }

    private static async Task EnsureProductAsync(
        ICatalogRepository catalog,
        string sku,
        string name,
        decimal price,
        Guid categoryId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var normalized = ProductItem.NormalizeSku(sku);
        if (await catalog.GetProductBySkuAsync(normalized, cancellationToken) is not null)
            return;

        var product = ProductItem.Create(sku, name, description: null, price, "USD", categoryId, utcNow);
        await catalog.AddProductAsync(product, cancellationToken);
    }
}
