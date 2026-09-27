using Identity.Application.Abstractions;
using Identity.Application.Models;
using Identity.Domain.Users;
using BuildingBlocks.Application.Common;

namespace Identity.Application.Auth;

internal static class AuthenticationSession
{
    public static AuthenticationResult Issue(
        User user,
        string? ipAddress,
        IDateTimeProvider clock,
        IRefreshTokenService refreshTokens,
        IAccessTokenIssuer accessTokens)
    {
        var now = clock.UtcNow;
        var generated = refreshTokens.Generate();
        var refreshExpires = refreshTokens.GetExpiryUtc(now);
        user.IssueRefreshToken(generated.TokenHash, now, refreshExpires, ipAddress);
        var access = accessTokens.Issue(user);

        return new AuthenticationResult(
            user.Id,
            user.Email,
            user.Role.ToString(),
            access.Value,
            access.ExpiresAtUtc,
            generated.RawToken,
            refreshExpires);
    }
}
