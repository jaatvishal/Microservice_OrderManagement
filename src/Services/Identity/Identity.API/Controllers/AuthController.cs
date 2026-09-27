using System.IdentityModel.Tokens.Jwt;
using Identity.API.Contracts;
using Identity.API.Infrastructure;
using Identity.Application.Auth;
using Identity.Application.Auth.Login;
using Identity.Application.Auth.Refresh;
using Identity.Application.Auth.Register;
using Identity.Application.Auth.Revoke;
using Identity.Application.Models;
using Identity.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Identity.API.Authorization;

namespace Identity.API.Controllers;

[ApiController]
[NoStore]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IResult> Register(
        RegisterUserRequest request,
        RegisterUserHandler handler,
        CancellationToken cancellationToken)
    {
        // Role is intentionally absent from the request. Clients cannot self-promote to Admin.
        var result = await handler.Handle(
            new RegisterUserCommand(request.Email, request.Password, request.FirstName, request.LastName, ClientIp),
            cancellationToken);

        if (result.IsFailure)
            return ApiResults.FromError(result.Error);

        return Results.Created($"/api/users/{result.Value.UserId}", ToResponse(result.Value));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> Login(
        LoginRequest request,
        LoginHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new LoginCommand(request.Email, request.Password, ClientIp),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Value))
            : ApiResults.FromError(result.Error);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> Refresh(
        RefreshTokenRequest request,
        RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new RefreshSessionCommand(request.RefreshToken, ClientIp),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Value))
            : ApiResults.FromError(result.Error);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> Revoke(
        RevokeRefreshTokenRequest request,
        RevokeRefreshTokenHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RevokeRefreshTokenCommand(request.RefreshToken), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = IdentityPolicies.CustomerAccess)]
    [HttpPost("revoke-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> RevokeAll(
        RevokeUserSessionsHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return ApiResults.FromError(IdentityErrors.InvalidAccessToken);

        var result = await handler.Handle(new RevokeUserSessionsCommand(userId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = IdentityPolicies.CustomerAccess)]
    [HttpGet("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> Me(GetCurrentUserHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return ApiResults.FromError(IdentityErrors.InvalidAccessToken);

        var result = await handler.Handle(new GetCurrentUserQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private bool TryGetUserId(out Guid userId)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out userId);
    }

    private static AuthenticationResponse ToResponse(AuthenticationResult result) =>
        new(
            result.UserId,
            result.Email,
            result.Role,
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.RefreshToken,
            result.RefreshTokenExpiresAtUtc,
            TokenType: "Bearer");
}
