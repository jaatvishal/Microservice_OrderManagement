using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using FluentValidation;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth.Revoke;

public sealed record RevokeRefreshTokenCommand(string RefreshToken);

public sealed class RevokeRefreshTokenValidator : AbstractValidator<RevokeRefreshTokenCommand>
{
    public RevokeRefreshTokenValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class RevokeRefreshTokenHandler(
    IUserRepository users,
    IRefreshTokenService refreshTokens,
    IDateTimeProvider clock,
    IValidator<RevokeRefreshTokenCommand> validator,
    ILogger<RevokeRefreshTokenHandler> logger)
{
    public async Task<Result> Handle(RevokeRefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(IdentityErrors.Validation("Refresh token is required."));

        var tokenHash = refreshTokens.Hash(command.RefreshToken);
        var user = await users.GetByRefreshTokenHashForUpdateAsync(tokenHash, cancellationToken);
        if (user is null)
        {
            // Same response as a real revoke so the endpoint cannot be used to probe tokens.
            return Result.Success();
        }

        if (user.RevokeRefreshToken(tokenHash, clock.UtcNow))
        {
            await users.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Revoked a refresh token for user {UserId}", user.Id);
        }

        return Result.Success();
    }
}
