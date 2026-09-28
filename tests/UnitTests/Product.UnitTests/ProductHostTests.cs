using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Product.UnitTests;

public class ProductHostTests
{
    [Fact]
    public async Task Invalid_sort_is_rejected_without_a_database_error()
    {
        await using var factory = new ProductApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products?sort=drop-table");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Product.Validation", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_without_a_token_is_unauthorized()
    {
        await using var factory = new ProductApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            sku = "BOOK-001",
            name = "Primer",
            price = 10,
            currency = "USD",
            categoryId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_token_cannot_create_a_product()
    {
        await using var factory = new ProductApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken("Customer"));

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            sku = "BOOK-001",
            name = "Primer",
            price = 10,
            currency = "USD",
            categoryId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Live_health_does_not_require_the_database()
    {
        await using var factory = new ProductApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Host_refuses_to_start_when_the_signing_key_is_too_short()
    {
        using var factory = new ShortKeyProductApiFactory();
        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("SigningKey", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ProductApiFactory : WebApplicationFactory<Program>
{
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string CreateAccessToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken(
            issuer: "ecommerce-identity",
            audience: "ecommerce-api",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim("role", role)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        SetTestConfiguration(SigningKey);
        return base.CreateHost(builder);
    }

    internal static void SetTestConfiguration(string signingKey)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__ProductDb",
            "Server=127.0.0.1,1433;Database=ProductDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=1");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", signingKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ecommerce-identity");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ecommerce-api");
        Environment.SetEnvironmentVariable("Jwt__ClockSkewSeconds", "30");
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "false");
        Environment.SetEnvironmentVariable("Database__SeedSampleCatalog", "false");
    }
}

internal sealed class ShortKeyProductApiFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ProductApiFactory.SetTestConfiguration("too-short");
        return base.CreateHost(builder);
    }
}
