using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Product.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef` only. The fallback uses integrated security so this file contains no password.
/// </summary>
public sealed class ProductDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
{
    public ProductDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ProductDb")
            ?? "Server=localhost,1433;Database=ProductDb;Integrated Security=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ProductDbContext(options);
    }
}
