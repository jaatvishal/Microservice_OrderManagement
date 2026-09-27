namespace Identity.API.Contracts;

public sealed record RegisterUserRequest(string Email, string Password, string FirstName, string LastName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RevokeRefreshTokenRequest(string RefreshToken);

public sealed record AuthenticationResponse(
    Guid UserId,
    string Email,
    string Role,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string TokenType);
