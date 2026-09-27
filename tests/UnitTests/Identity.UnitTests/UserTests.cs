using Identity.Domain.Users;

namespace Identity.UnitTests;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_normalizes_email_and_defaults_to_customer()
    {
        var user = User.Register("  User@Email.com ", "hash", "Ava", "Stone", Now);

        Assert.Equal("User@Email.com", user.Email);
        Assert.Equal("USER@EMAIL.COM", user.NormalizedEmail);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Register_rejects_a_missing_password_hash()
    {
        var act = () => User.Register("user@email.com", " ", "Ava", "Stone", Now);
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Rotate_replaces_the_presented_token_and_leaves_other_sessions_active()
    {
        var user = User.Register("user@email.com", "hash", "Ava", "Stone", Now);
        user.IssueRefreshToken("hash-a", Now, Now.AddDays(7), null);
        user.IssueRefreshToken("hash-b", Now, Now.AddDays(7), null);

        var rotation = user.RotateRefreshToken("hash-a", "hash-a2", Now, Now.AddDays(7), null, TimeSpan.FromSeconds(30));

        Assert.Equal(RefreshTokenRotationStatus.Success, rotation.Status);
        Assert.False(user.RefreshTokens.Single(token => token.TokenHash == "hash-a").IsActive(Now));
        Assert.True(user.RefreshTokens.Single(token => token.TokenHash == "hash-b").IsActive(Now));
        Assert.True(user.RefreshTokens.Single(token => token.TokenHash == "hash-a2").IsActive(Now));
    }

    [Fact]
    public void Reuse_inside_the_grace_window_does_not_revoke_the_successor()
    {
        var user = User.Register("user@email.com", "hash", "Ava", "Stone", Now);
        user.IssueRefreshToken("hash-a", Now, Now.AddDays(7), null);
        user.RotateRefreshToken("hash-a", "hash-a2", Now, Now.AddDays(7), null, TimeSpan.FromSeconds(30));

        var replay = user.RotateRefreshToken(
            "hash-a",
            "hash-ignored",
            Now.AddSeconds(10),
            Now.AddDays(7),
            null,
            TimeSpan.FromSeconds(30));

        Assert.Equal(RefreshTokenRotationStatus.RotatedWithinGrace, replay.Status);
        Assert.True(user.RefreshTokens.Single(token => token.TokenHash == "hash-a2").IsActive(Now.AddSeconds(10)));
    }

    [Fact]
    public void Reuse_outside_the_grace_window_revokes_every_active_session()
    {
        var user = User.Register("user@email.com", "hash", "Ava", "Stone", Now);
        user.IssueRefreshToken("hash-a", Now, Now.AddDays(7), null);
        user.IssueRefreshToken("hash-b", Now, Now.AddDays(7), null);
        user.RotateRefreshToken("hash-a", "hash-a2", Now, Now.AddDays(7), null, TimeSpan.FromSeconds(30));

        var replay = user.RotateRefreshToken(
            "hash-a",
            "hash-ignored",
            Now.AddMinutes(2),
            Now.AddDays(7),
            null,
            TimeSpan.FromSeconds(30));

        Assert.Equal(RefreshTokenRotationStatus.ReuseDetected, replay.Status);
        Assert.All(user.RefreshTokens, token => Assert.False(token.IsActive(Now.AddMinutes(2))));
    }

    [Fact]
    public void Expired_token_does_not_revoke_other_sessions()
    {
        var user = User.Register("user@email.com", "hash", "Ava", "Stone", Now);
        user.IssueRefreshToken("hash-old", Now.AddDays(-8), Now.AddDays(-1), null);
        user.IssueRefreshToken("hash-current", Now, Now.AddDays(7), null);

        var rotation = user.RotateRefreshToken(
            "hash-old",
            "hash-new",
            Now,
            Now.AddDays(7),
            null,
            TimeSpan.FromSeconds(30));

        Assert.Equal(RefreshTokenRotationStatus.Invalid, rotation.Status);
        Assert.True(user.RefreshTokens.Single(token => token.TokenHash == "hash-current").IsActive(Now));
    }

    [Fact]
    public void Deactivate_revokes_active_refresh_tokens()
    {
        var user = User.Register("user@email.com", "hash", "Ava", "Stone", Now);
        user.IssueRefreshToken("hash-a", Now, Now.AddDays(7), null);

        user.Deactivate(Now);

        Assert.False(user.IsActive);
        Assert.False(user.RefreshTokens.Single().IsActive(Now));
    }
}
