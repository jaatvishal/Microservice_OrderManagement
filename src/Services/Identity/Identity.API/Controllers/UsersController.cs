using Identity.API.Authorization;
using Identity.API.Infrastructure;
using Identity.Application.Models;
using Identity.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Authorize(Policy = IdentityPolicies.AdminOnly)]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedList<UserSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IResult> List(
        ListUsersHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await handler.Handle(new ListUsersQuery(page, pageSize), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IResult> Deactivate(
        Guid id,
        DeactivateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeactivateUserCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.FromError(result.Error);
    }
}
