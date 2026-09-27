using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth.Revoke;

public sealed record RevokeUserSessionsCommand(Guid UserId);

public sealed class RevokeUserSessionsHandler(
    IUserRepository users,
    IDateTimeProvider clock,
    ILogger<RevokeUserSessionsHandler> logger)
{
    public async Task<Result> Handle(RevokeUserSessionsCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdForUpdateAsync(command.UserId, cancellationToken);
        if (user is null)
            return Result.Success();

        user.RevokeActiveTokens(clock.UtcNow);
        await users.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Revoked active sessions for user {UserId}", user.Id);
        return Result.Success();
    }
}
