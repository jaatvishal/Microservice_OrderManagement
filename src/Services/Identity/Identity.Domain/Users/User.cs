namespace Identity.Domain.Users;

public sealed class User
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string normalizedEmail,
        string passwordHash,
        string firstName,
        string lastName,
        UserRole role,
        DateTime createdAtUtc)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Visible to the application and persistence layers inside this service.
    /// HTTP contracts must never return it.
    /// </summary>
    internal string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    public static User Register(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        DateTime utcNow,
        UserRole role = UserRole.Customer)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("Email is invalid.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        var trimmedEmail = email.Trim();
        return new User(
            Guid.NewGuid(),
            trimmedEmail,
            trimmedEmail.ToUpperInvariant(),
            passwordHash,
            firstName.Trim(),
            lastName.Trim(),
            role,
            utcNow);
    }

    public void ReplacePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        PasswordHash = passwordHash;
    }

    public RefreshToken IssueRefreshToken(
        string tokenHash,
        DateTime utcNow,
        DateTime expiresAtUtc,
        string? createdByIp)
    {
        var token = RefreshToken.Issue(Id, tokenHash, utcNow, expiresAtUtc, createdByIp);
        _refreshTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Rotates a refresh token. Presenting a token that was already rotated outside the
    /// grace window is treated as theft: every active session for this user is revoked.
    /// A short grace window avoids logging the user out when two refreshes race or a
    /// response is retried immediately. It does not re-issue the successor token.
    /// </summary>
    public RefreshTokenRotationResult RotateRefreshToken(
        string currentTokenHash,
        string newTokenHash,
        DateTime utcNow,
        DateTime newExpiresAtUtc,
        string? ipAddress,
        TimeSpan reuseGrace)
    {
        var current = _refreshTokens.SingleOrDefault(token => token.TokenHash == currentTokenHash);
        if (current is null || !IsActive)
            return RefreshTokenRotationResult.Invalid();

        if (!current.IsActive(utcNow))
        {
            if (current.WasRotated && utcNow - current.RevokedAtUtc!.Value <= reuseGrace)
                return RefreshTokenRotationResult.RotatedWithinGrace();

            if (current.WasRotated)
            {
                RevokeActiveTokens(utcNow);
                return RefreshTokenRotationResult.ReuseDetected();
            }

            return RefreshTokenRotationResult.Invalid();
        }

        var replacement = RefreshToken.Issue(Id, newTokenHash, utcNow, newExpiresAtUtc, ipAddress);
        current.Revoke(utcNow, newTokenHash);
        _refreshTokens.Add(replacement);
        return RefreshTokenRotationResult.Succeeded(replacement);
    }

    public bool RevokeRefreshToken(string tokenHash, DateTime utcNow)
    {
        var token = _refreshTokens.SingleOrDefault(candidate => candidate.TokenHash == tokenHash);
        if (token is null || token.RevokedAtUtc is not null)
            return false;

        token.Revoke(utcNow, replacedByTokenHash: null);
        return true;
    }

    public void RevokeActiveTokens(DateTime utcNow)
    {
        foreach (var token in _refreshTokens.Where(token => token.IsActive(utcNow)))
            token.Revoke(utcNow, replacedByTokenHash: null);
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        RevokeActiveTokens(utcNow);
    }
}
