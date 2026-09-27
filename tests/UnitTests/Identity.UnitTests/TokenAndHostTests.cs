using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;
using Identity.API.Authorization;
using Identity.API.Controllers;
using Identity.Domain.Users;
using Identity.Infrastructure.Security;
using Identity.UnitTests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.UnitTests;

public class TokenAndHostTests
{
    [Fact]
    public void Access_token_carries_subject_and_role_without_inbound_claim_mapping()
    {
        var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var clock = new FixedClock { UtcNow = DateTime.UtcNow.AddMinutes(-1) };
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(new JwtOptions
            {
                Issuer = "ecommerce-identity",
                Audience = "ecommerce-api",
                SigningKey = signingKey,
                AccessTokenMinutes = 15
            }),
            clock);
        var user = User.Register("admin@example.com", "hash", "Platform", "Admin", clock.UtcNow, UserRole.Admin);

        var issued = issuer.Issue(user);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(issued.Value, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "ecommerce-identity",
            ValidAudience = "ecommerce-api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = "role",
            NameClaimType = JwtRegisteredClaimNames.Sub
        }, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        Assert.Equal("Admin", principal.FindFirst("role")!.Value);
        Assert.True(principal.IsInRole("Admin"));
    }

    [Fact]
    public void Password_hasher_rejects_a_wrong_password_and_a_missing_hash()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("Password1");

        Assert.NotEqual("Password1", hash);
        Assert.Equal(PasswordVerification.Success, hasher.Verify("Password1", hash));
        Assert.Equal(PasswordVerification.Failed, hasher.Verify("OtherPass1", hash));
        Assert.Equal(PasswordVerification.Failed, hasher.Verify("Password1", null));
    }

    [Fact]
    public void Admin_endpoints_require_the_admin_policy_and_registration_stays_anonymous()
    {
        var register = typeof(AuthController).GetMethod(nameof(AuthController.Register));

        Assert.Equal(
            IdentityPolicies.AdminOnly,
            typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.NotNull(register!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task Register_rejects_invalid_input_without_a_database_exception()
    {
        await using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "short",
            firstName = "",
            lastName = ""
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Identity.Validation", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_without_a_token_is_unauthorized()
    {
        await using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("access token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customer_token_cannot_list_users()
    {
        await using var factory = new IdentityApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(UserRole.Customer));

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Host_refuses_to_start_when_the_signing_key_is_too_short()
    {
        using var factory = new ShortKeyApiFactory();

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("SigningKey", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string CreateAccessToken(UserRole role)
    {
        var clock = new FixedClock { UtcNow = DateTime.UtcNow.AddMinutes(-1) };
        var user = User.Register("customer@example.com", "hash", "Ava", "Stone", clock.UtcNow, role);
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(new JwtOptions
            {
                Issuer = "ecommerce-identity",
                Audience = "ecommerce-api",
                SigningKey = SigningKey,
                AccessTokenMinutes = 15
            }),
            clock);
        return issuer.Issue(user).Value;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Environment variables are loaded after appsettings, so they override the
        // empty secrets in source control. In-memory sources added too early lose that race.
        SetTestConfiguration(SigningKey);
        return base.CreateHost(builder);
    }

    internal static void SetTestConfiguration(string signingKey)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__IdentityDb",
            "Server=127.0.0.1,1433;Database=IdentityDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=1");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", signingKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ecommerce-identity");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ecommerce-api");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "15");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "7");
        Environment.SetEnvironmentVariable("Jwt__RefreshReuseGraceSeconds", "30");
        Environment.SetEnvironmentVariable("Jwt__ClockSkewSeconds", "30");
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "false");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", null);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", null);
    }
}

internal sealed class ShortKeyApiFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        IdentityApiFactory.SetTestConfiguration("too-short");
        return base.CreateHost(builder);
    }
}
