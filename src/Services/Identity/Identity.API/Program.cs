using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Identity.API.Authorization;
using Identity.API.Infrastructure;
using Identity.Application;
using Identity.Application.Auth;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    builder.Services.AddIdentityApplication();
    builder.Services.AddIdentityInfrastructure(builder.Configuration);
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length > 0)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("web", policy =>
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });
    }

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests.",
                Detail = "Authentication attempts are temporarily limited. Try again later.",
                Type = "https://httpstatuses.com/429"
            }, token);
        };

        // Partition on the connection address, not X-Forwarded-For.
        // Until the gateway is the only path in, a client can spoof that header and skip the limit.
        options.AddPolicy("auth", httpContext =>
        {
            var permit = builder.Configuration.GetValue("AuthRateLimit:PermitLimit", 20);
            var windowSeconds = builder.Configuration.GetValue("AuthRateLimit:WindowSeconds", 60);
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    });

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    var signingKeyBytes = Encoding.UTF8.GetBytes(jwt.SigningKey ?? string.Empty);
    if (signingKeyBytes.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey must be at least 32 bytes. Supply it through environment variables, user-secrets, or Azure Key Vault.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                ClockSkew = TimeSpan.FromSeconds(Math.Clamp(jwt.ClockSkewSeconds, 0, 60)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(ApiResults.UnauthorizedBody());
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(ApiResults.ForbiddenBody());
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(IdentityPolicies.CustomerAccess, policy =>
            policy.RequireRole(IdentityRoles.Customer, IdentityRoles.Admin));
        options.AddPolicy(IdentityPolicies.AdminOnly, policy =>
            policy.RequireRole(IdentityRoles.Admin));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Identity Service",
            Version = "v1",
            Description = "Accounts, access tokens, and refresh tokens for the order platform."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token only. Swagger adds the Bearer prefix."
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<IdentityDbContext>("identity-db", tags: ["ready"]);

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (allowedOrigins.Length > 0)
        app.UseCors("web");

    // TLS is expected to terminate at the gateway or Azure Front Door.
    // Redirecting inside the container breaks health probes that call HTTP on the pod.
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

    await IdentityDatabaseInitializer.InitializeAsync(
        app.Services,
        app.Configuration,
        allowDevelopmentAdminSeed: app.Environment.IsDevelopment(),
        CancellationToken.None);

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Identity service failed to start.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
