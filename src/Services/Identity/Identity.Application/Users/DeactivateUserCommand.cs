using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Users;

public sealed record DeactivateUserCommand(Guid UserId);

public sealed class DeactivateUserHandler(
    IUserRepository users,
    IDateTimeProvider clock,
    ILogger<DeactivateUserHandler> logger)
{
    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdForUpdateAsync(command.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(IdentityErrors.NotFound("User was not found."));

        user.Deactivate(clock.UtcNow);
        await users.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deactivated user {UserId}", user.Id);
        return Result.Success();
    }
}
