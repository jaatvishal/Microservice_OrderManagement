using System.Text;
using BuildingBlocks.Application.Common;
using BuildingBlocks.Infrastructure.Common;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:IdentityDb is required. Set ConnectionStrings__IdentityDb from user-secrets, the environment, or Azure Key Vault. Do not commit credentials.");
        }

        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // Azure SQL drops idle connections and fails over. The retry policy covers those
                // transient errors. Register stays safe under a retried insert because the email
                // unique index rejects the duplicate.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= 32,
                "Jwt:SigningKey must be at least 32 bytes. Supply it through environment variables, user-secrets, or Azure Key Vault.")
            .Validate(options => options.AccessTokenMinutes is >= 1 and <= 60, "Jwt:AccessTokenMinutes must be between 1 and 60.")
            .Validate(options => options.RefreshTokenDays is >= 1 and <= 30, "Jwt:RefreshTokenDays must be between 1 and 30.")
            .Validate(options => options.RefreshReuseGraceSeconds is >= 0 and <= 120, "Jwt:RefreshReuseGraceSeconds must be between 0 and 120.")
            .Validate(options => options.ClockSkewSeconds is >= 0 and <= 60, "Jwt:ClockSkewSeconds must be between 0 and 60.")
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
