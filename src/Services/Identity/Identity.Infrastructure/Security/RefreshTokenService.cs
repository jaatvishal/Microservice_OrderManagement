using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

public sealed class RefreshTokenService(IOptions<JwtOptions> options) : IRefreshTokenService
{
    public TimeSpan ReuseGrace => TimeSpan.FromSeconds(options.Value.RefreshReuseGraceSeconds);

    public RefreshTokenValue Generate()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new RefreshTokenValue(raw, Hash(raw));
    }

    public string Hash(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    public DateTime GetExpiryUtc(DateTime utcNow) => utcNow.AddDays(options.Value.RefreshTokenDays);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
