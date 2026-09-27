using BuildingBlocks.Application.Models;
using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Identity.Application.Models;

namespace Identity.Application.Users;

public sealed record GetCurrentUserQuery(Guid UserId);

public sealed class GetCurrentUserHandler(IUserRepository users)
{
    public async Task<Result<UserProfile>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var profile = await users.GetProfileAsync(query.UserId, cancellationToken);
        if (profile is null || !profile.IsActive)
            return Result.Failure<UserProfile>(IdentityErrors.InvalidAccessToken);

        return Result.Success(profile);
    }
}
