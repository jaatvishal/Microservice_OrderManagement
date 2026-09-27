using BuildingBlocks.Application.Common;
using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

public static class IdentityDatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        bool allowDevelopmentAdminSeed,
        CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Database:ApplyMigrations", false))
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentityDatabaseInitializer));
            logger.LogInformation("Applying Identity database migrations.");

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
        }

        await SeedDevelopmentAdminAsync(services, configuration, allowDevelopmentAdminSeed, cancellationToken);
    }

    private static async Task SeedDevelopmentAdminAsync(
        IServiceProvider services,
        IConfiguration configuration,
        bool allowDevelopmentAdminSeed,
        CancellationToken cancellationToken)
    {
        // A seed password in production configuration would be a standing backdoor.
        // Production admins are created by an operational process, not by app startup.
        if (!allowDevelopmentAdminSeed)
            return;

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentityDatabaseInitializer));
        if (password.Length < 8)
        {
            logger.LogWarning("Skipped development admin seed because the password does not meet the minimum length.");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var normalized = email.Trim().ToUpperInvariant();
        if (await users.GetByNormalizedEmailForUpdateAsync(normalized, cancellationToken) is not null)
            return;

        var admin = User.Register(email, hasher.Hash(password), "Platform", "Admin", clock.UtcNow, UserRole.Admin);
        await users.AddAsync(admin, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded the development admin account.");
    }
}
