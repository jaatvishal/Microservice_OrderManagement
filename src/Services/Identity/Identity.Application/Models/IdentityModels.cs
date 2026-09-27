namespace Identity.Application.Models;

public sealed record AuthenticationResult(
    Guid UserId,
    string Email,
    string Role,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record UserProfile(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive);

public sealed record UserSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record PagedList<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
