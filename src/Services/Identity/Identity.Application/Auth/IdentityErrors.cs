using BuildingBlocks.Application.Models;

namespace Identity.Application.Auth;

public static class IdentityErrors
{
    public static Error Validation(string message) => Error.Validation("Identity.Validation", message);

    public static Error EmailConflict() =>
        Error.Conflict("Identity.EmailConflict", "An account with this email already exists.");

    public static Error NotFound(string message) => Error.NotFound("Identity.NotFound", message);

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Identity.Unauthorized", "Invalid email or password.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Identity.Unauthorized", "Refresh token is invalid.");

    public static readonly Error InvalidAccessToken =
        Error.Unauthorized("Identity.Unauthorized", "The access token is invalid.");
}
