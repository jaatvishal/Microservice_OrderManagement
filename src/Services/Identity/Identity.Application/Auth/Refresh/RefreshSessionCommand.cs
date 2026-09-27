using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using FluentValidation;
using Identity.Application.Abstractions;
using Identity.Application.Models;
using Identity.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth.Refresh;

public sealed record RefreshSessionCommand(string RefreshToken, string? IpAddress);

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class RefreshSessionHandler(
    IUserRepository users,
    IRefreshTokenService refreshTokens,
    IAccessTokenIssuer accessTokens,
    IDateTimeProvider clock,
    IValidator<RefreshSessionCommand> validator,
    ILogger<RefreshSessionHandler> logger)
{
    public async Task<Result<AuthenticationResult>> Handle(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);

        var tokenHash = refreshTokens.Hash(command.RefreshToken);
        var user = await users.GetByRefreshTokenHashForUpdateAsync(tokenHash, cancellationToken);
        if (user is null)
            return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);

        var now = clock.UtcNow;
        var generated = refreshTokens.Generate();
        var rotation = user.RotateRefreshToken(
            tokenHash,
            generated.TokenHash,
            now,
            refreshTokens.GetExpiryUtc(now),
            command.IpAddress,
            refreshTokens.ReuseGrace);

        switch (rotation.Status)
        {
            case RefreshTokenRotationStatus.RotatedWithinGrace:
                logger.LogInformation(
                    "Refresh token for user {UserId} was presented again inside the reuse grace window",
                    user.Id);
                return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);

            case RefreshTokenRotationStatus.Invalid:
                return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);

            case RefreshTokenRotationStatus.ReuseDetected:
                await users.SaveChangesAsync(cancellationToken);
                logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}. Active sessions were revoked.",
                    user.Id);
                return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);

            case RefreshTokenRotationStatus.Success:
                try
                {
                    await users.SaveChangesAsync(cancellationToken);
                }
                catch (IdentityConcurrencyException exception)
                {
                    // Two refreshes updated the same row. Do not treat that as theft:
                    // the winner already holds the successor token.
                    logger.LogWarning(exception, "Concurrent refresh for user {UserId}", user.Id);
                    return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);
                }

                var access = accessTokens.Issue(user);
                return Result.Success(new AuthenticationResult(
                    user.Id,
                    user.Email,
                    user.Role.ToString(),
                    access.Value,
                    access.ExpiresAtUtc,
                    generated.RawToken,
                    rotation.Token!.ExpiresAtUtc));

            default:
                return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidRefreshToken);
        }
    }
}
