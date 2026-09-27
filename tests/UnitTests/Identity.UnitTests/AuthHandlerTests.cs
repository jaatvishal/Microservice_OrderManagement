using FluentValidation;
using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Identity.Application.Auth.Login;
using Identity.Application.Auth.Refresh;
using Identity.Application.Auth.Register;
using Identity.Application.Auth.Revoke;
using Identity.Application.Users;
using Identity.Domain.Users;
using Identity.UnitTests.Support;

namespace Identity.UnitTests;

public class AuthHandlerTests
{
    private readonly FixedClock _clock = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _passwords = new();
    private readonly FakeRefreshTokenService _refreshTokens = new();
    private readonly FakeAccessTokenIssuer _accessTokens = new();

    [Fact]
    public async Task Register_creates_a_customer_and_does_not_store_the_raw_password_or_refresh_token()
    {
        var handler = CreateRegisterHandler();

        var result = await handler.Handle(
            new RegisterUserCommand("ava@example.com", "Password1", "Ava", "Stone", "127.0.0.1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Customer", result.Value.Role);
        var user = Assert.Single(_users.Users);
        Assert.NotEqual("Password1", user.PasswordHash);
        Assert.DoesNotContain(user.RefreshTokens, token => token.TokenHash == result.Value.RefreshToken);
        Assert.Contains(user.RefreshTokens, token => token.TokenHash == "hash:" + result.Value.RefreshToken);
    }

    [Fact]
    public async Task Register_rejects_a_weak_password_before_creating_a_user()
    {
        var handler = CreateRegisterHandler();

        var result = await handler.Handle(
            new RegisterUserCommand("ava@example.com", "password", "Ava", "Stone", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.Validation", result.Error.Code);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        var handler = CreateRegisterHandler();
        await handler.Handle(
            new RegisterUserCommand("ava@example.com", "Password1", "Ava", "Stone", null),
            CancellationToken.None);

        var duplicate = await handler.Handle(
            new RegisterUserCommand("AVA@example.com", "Password1", "Ava", "Stone", null),
            CancellationToken.None);

        Assert.Equal("Identity.EmailConflict", duplicate.Error.Code);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Register_maps_a_uniqueness_race_to_a_conflict()
    {
        _users.SaveException = new EmailAlreadyRegisteredException();
        var handler = CreateRegisterHandler();

        var result = await handler.Handle(
            new RegisterUserCommand("ava@example.com", "Password1", "Ava", "Stone", null),
            CancellationToken.None);

        Assert.Equal("Identity.EmailConflict", result.Error.Code);
    }

    [Fact]
    public async Task Login_unknown_user_and_wrong_password_return_the_same_error()
    {
        var handler = CreateLoginHandler();
        await CreateRegisterHandler().Handle(
            new RegisterUserCommand("ava@example.com", "Password1", "Ava", "Stone", null),
            CancellationToken.None);

        var unknown = await handler.Handle(new LoginCommand("missing@example.com", "Password1", null), CancellationToken.None);
        var wrong = await handler.Handle(new LoginCommand("ava@example.com", "WrongPass1", null), CancellationToken.None);

        Assert.Equal(unknown.Error, wrong.Error);
        Assert.Equal("Invalid email or password.", unknown.Error.Message);
    }

    [Fact]
    public async Task Login_upgrades_the_password_hash_when_rehash_is_required()
    {
        var user = User.Register("ava@example.com", "hashed:Password1", "Ava", "Stone", _clock.UtcNow);
        await _users.AddAsync(user, CancellationToken.None);
        _passwords.NextVerification = PasswordVerification.SuccessRehashNeeded;
        var hashesBefore = _passwords.HashCalls;

        var result = await CreateLoginHandler().Handle(
            new LoginCommand("ava@example.com", "Password1", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_passwords.HashCalls > hashesBefore);
        Assert.Equal("hashed:1:Password1", user.PasswordHash);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_rejects_reuse_after_the_grace_window()
    {
        var registered = await CreateRegisterHandler().Handle(
            new RegisterUserCommand("ava@example.com", "Password1", "Ava", "Stone", null),
            CancellationToken.None);
        var handler = CreateRefreshHandler();

        var rotated = await handler.Handle(
            new RefreshSessionCommand(registered.Value.RefreshToken, null),
            CancellationToken.None);

        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(registered.Value.RefreshToken, rotated.Value.RefreshToken);

        _clock.UtcNow = _clock.UtcNow.AddMinutes(2);
        var replay = await handler.Handle(
            new RefreshSessionCommand(registered.Value.RefreshToken, null),
            CancellationToken.None);

        Assert.Equal("Identity.Unauthorized", replay.Error.Code);
        var user = Assert.Single(_users.Users);
        Assert.All(user.RefreshTokens, token => Assert.False(token.IsActive(_clock.UtcNow)));
    }

    [Fact]
    public async Task Revoke_of_an_unknown_token_still_succeeds()
    {
        var result = await CreateRevokeHandler().Handle(
            new RevokeRefreshTokenCommand("does-not-exist"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Deactivate_then_login_is_rejected()
    {
        var registered = await CreateRegisterHandler().Handle(
            new RegisterUserCommand("ava@example.com", "Password1", "Ava", "Stone", null),
            CancellationToken.None);

        var deactivate = await new DeactivateUserHandler(_users, _clock, NullLog.For<DeactivateUserHandler>())
            .Handle(new DeactivateUserCommand(registered.Value.UserId), CancellationToken.None);

        var login = await CreateLoginHandler().Handle(
            new LoginCommand("ava@example.com", "Password1", null),
            CancellationToken.None);

        Assert.True(deactivate.IsSuccess);
        Assert.Equal("Invalid email or password.", login.Error.Message);
    }

    [Fact]
    public async Task List_rejects_an_unbounded_page_size()
    {
        var result = await new ListUsersHandler(_users).Handle(new ListUsersQuery(1, 500), CancellationToken.None);

        Assert.Equal("Identity.Validation", result.Error.Code);
    }

    private RegisterUserHandler CreateRegisterHandler() =>
        new(
            _users,
            _passwords,
            _refreshTokens,
            _accessTokens,
            _clock,
            new RegisterUserValidator(),
            NullLog.For<RegisterUserHandler>());

    private LoginHandler CreateLoginHandler() =>
        new(
            _users,
            _passwords,
            _refreshTokens,
            _accessTokens,
            _clock,
            new LoginValidator(),
            NullLog.For<LoginHandler>());

    private RefreshSessionHandler CreateRefreshHandler() =>
        new(
            _users,
            _refreshTokens,
            _accessTokens,
            _clock,
            new RefreshSessionValidator(),
            NullLog.For<RefreshSessionHandler>());

    private RevokeRefreshTokenHandler CreateRevokeHandler() =>
        new(
            _users,
            _refreshTokens,
            _clock,
            new RevokeRefreshTokenValidator(),
            NullLog.For<RevokeRefreshTokenHandler>());
}
