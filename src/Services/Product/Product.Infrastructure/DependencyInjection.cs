using System.Text;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Abstractions;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Security;

namespace Product.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ProductDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:ProductDb is required. Set ConnectionStrings__ProductDb from user-secrets, the environment, or Azure Key Vault. Do not commit credentials.");
        }

        services.AddDbContext<ProductDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // A retried insert cannot create two SKUs. The unique index rejects the duplicate.
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            });
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= 32,
                "Jwt:SigningKey must be at least 32 bytes. Use the same key Identity uses to sign access tokens.")
            .Validate(options => options.ClockSkewSeconds is >= 0 and <= 60, "Jwt:ClockSkewSeconds must be between 0 and 60.")
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        return services;
    }
}
