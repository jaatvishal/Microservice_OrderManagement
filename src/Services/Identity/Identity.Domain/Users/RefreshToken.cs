namespace Identity.Domain.Users;

/// <summary>
/// A single login session secret. Only the hash is stored.
/// The raw token exists solely in the HTTP response that issued it.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? createdByIp)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public string? CreatedByIp { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public bool WasRotated => RevokedAtUtc is not null && ReplacedByTokenHash is not null;

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? createdByIp)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Refresh token expiry must be in the future.", nameof(expiresAtUtc));

        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, createdAtUtc, expiresAtUtc, createdByIp);
    }

    public void Revoke(DateTime utcNow, string? replacedByTokenHash)
    {
        if (RevokedAtUtc is not null)
            return;

        RevokedAtUtc = utcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
