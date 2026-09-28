using BuildingBlocks.Contracts.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Product.API.Authorization;
using Product.API.Contracts;
using Product.API.Infrastructure;
using Product.Application.Catalog;

namespace Product.API.Controllers;

[ApiController]
[EnableRateLimiting("catalog")]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IResult> List(
        ListCategoriesHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool activeOnly = true)
    {
        // Shoppers always receive the public menu. Only an admin token may include inactive categories.
        var activeOnlyFilter = User.IsInRole(PlatformRoles.Admin) ? activeOnly : true;
        var result = await handler.Handle(new ListCategoriesQuery(page, pageSize, activeOnlyFilter), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPost]
    public async Task<IResult> Create(
        CreateCategoryRequest request,
        CreateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CreateCategoryCommand(request.Name), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/categories/{result.Value.Id}", result.Value)
            : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPut("{id:guid}")]
    public async Task<IResult> Rename(
        Guid id,
        RenameCategoryRequest request,
        RenameCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new RenameCategoryCommand(id, request.Name), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IResult> Deactivate(
        Guid id,
        DeactivateCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeactivateCategoryCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.FromError(result.Error);
    }
}
