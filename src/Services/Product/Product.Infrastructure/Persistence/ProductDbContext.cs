using Microsoft.EntityFrameworkCore;
using Product.Domain.Catalog;

namespace Product.Infrastructure.Persistence;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ProductItem> Products => Set<ProductItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductDbContext).Assembly);
}
