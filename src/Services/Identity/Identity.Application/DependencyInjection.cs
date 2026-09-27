using FluentValidation;
using Identity.Application.Auth.Login;
using Identity.Application.Auth.Refresh;
using Identity.Application.Auth.Register;
using Identity.Application.Auth.Revoke;
using Identity.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<RevokeRefreshTokenHandler>();
        services.AddScoped<RevokeUserSessionsHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<DeactivateUserHandler>();
        services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
        return services;
    }
}
