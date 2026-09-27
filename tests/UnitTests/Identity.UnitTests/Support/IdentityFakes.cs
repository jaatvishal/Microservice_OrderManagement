using BuildingBlocks.Application.Common;
using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Identity.Application.Models;
using Identity.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.UnitTests.Support;

internal sealed class FixedClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int HashCalls { get; private set; }

    public PasswordVerification NextVerification { get; set; } = PasswordVerification.Success;

    public string Hash(string password)
    {
        HashCalls++;
        return $"hashed:{HashCalls}:{password}";
    }

    public PasswordVerification Verify(string password, string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return PasswordVerification.Failed;

        var matches = passwordHash == "hashed:" + password
            || (passwordHash.StartsWith("hashed:", StringComparison.Ordinal)
                && passwordHash.EndsWith(":" + password, StringComparison.Ordinal));

        if (!matches)
            return PasswordVerification.Failed;

        if (NextVerification == PasswordVerification.SuccessRehashNeeded && passwordHash == "hashed:" + password)
            return PasswordVerification.SuccessRehashNeeded;

        return PasswordVerification.Success;
    }
}

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessToken Issue(User user) =>
        new($"access:{user.Id}:{user.Role}", DateTime.UtcNow.AddMinutes(15));
}

internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    private int _counter;

    public TimeSpan ReuseGrace { get; } = TimeSpan.FromSeconds(30);

    public RefreshTokenValue Generate()
    {
        var raw = $"raw-{++_counter}";
        return new RefreshTokenValue(raw, Hash(raw));
    }

    public string Hash(string rawToken) => "hash:" + rawToken;

    public DateTime GetExpiryUtc(DateTime utcNow) => utcNow.AddDays(7);
}

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public Exception? SaveException { get; set; }

    public IReadOnlyList<User> Users => _users;

    public Task<User?> GetByNormalizedEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));

    public Task<User?> GetByRefreshTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(user => user.RefreshTokens.Any(token => token.TokenHash == tokenHash)));

    public Task<User?> GetByIdForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_users.SingleOrDefault(user => user.Id == userId));

    public Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = _users.SingleOrDefault(candidate => candidate.Id == userId);
        UserProfile? profile = user is null
            ? null
            : new UserProfile(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString(), user.IsActive);
        return Task.FromResult(profile);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (SaveException is not null)
            throw SaveException;

        return Task.CompletedTask;
    }

    public Task<PagedList<UserSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = _users
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserSummary(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role.ToString(),
                user.IsActive,
                user.CreatedAtUtc))
            .ToList();

        return Task.FromResult(new PagedList<UserSummary>(items, page, pageSize, _users.Count));
    }
}

internal static class NullLog
{
    public static Microsoft.Extensions.Logging.ILogger<T> For<T>() => NullLogger<T>.Instance;
}
