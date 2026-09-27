using BuildingBlocks.Application.Models;
using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Identity.Application.Models;

namespace Identity.Application.Users;

public sealed record ListUsersQuery(int Page, int PageSize);

public sealed class ListUsersHandler(IUserRepository users)
{
    public async Task<Result<PagedList<UserSummary>>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            return Result.Failure<PagedList<UserSummary>>(
                IdentityErrors.Validation("Page must be at least 1 and page size must be between 1 and 100."));
        }

        var page = await users.ListAsync(query.Page, query.PageSize, cancellationToken);
        return Result.Success(page);
    }
}
