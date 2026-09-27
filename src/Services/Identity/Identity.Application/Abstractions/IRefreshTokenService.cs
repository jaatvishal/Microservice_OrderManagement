namespace Identity.Application.Abstractions;

public interface IRefreshTokenService
{
    RefreshTokenValue Generate();

    string Hash(string rawToken);

    DateTime GetExpiryUtc(DateTime utcNow);

    TimeSpan ReuseGrace { get; }
}

public sealed record RefreshTokenValue(string RawToken, string TokenHash);
