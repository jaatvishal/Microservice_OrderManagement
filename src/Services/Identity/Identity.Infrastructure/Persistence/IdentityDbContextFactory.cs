using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef` only. Runtime configuration comes from the API host.
/// The fallback uses integrated security so this file does not contain a password.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDb")
            ?? "Server=localhost,1433;Database=IdentityDb;Integrated Security=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new IdentityDbContext(options);
    }
}
