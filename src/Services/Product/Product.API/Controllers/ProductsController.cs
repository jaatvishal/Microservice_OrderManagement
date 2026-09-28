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
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IResult> List(
        ListProductsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool activeOnly = true)
    {
        if (!TryParseSort(sort, out var parsedSort))
            return ApiResults.FromError(ProductErrors.Validation("Sort must be name, priceAsc, priceDesc, or newest."));

        var activeOnlyFilter = User.IsInRole(PlatformRoles.Admin) ? activeOnly : true;
        var result = await handler.Handle(
            new ListProductsQuery(new ProductSearch(page, pageSize, categoryId, search, parsedSort, activeOnlyFilter)),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IResult> Get(Guid id, GetProductHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new GetProductQuery(id, IncludeInactive: User.IsInRole(PlatformRoles.Admin)),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPost]
    public async Task<IResult> Create(
        CreateProductRequest request,
        CreateProductHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateProductCommand(request.Sku, request.Name, request.Description, request.Price, request.Currency, request.CategoryId),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/products/{result.Value.Id}", result.Value)
            : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPut("{id:guid}")]
    public async Task<IResult> Update(
        Guid id,
        UpdateProductRequest request,
        UpdateProductHandler handler,
        CancellationToken cancellationToken)
    {
        if (!RowVersionToken.TryDecode(request.RowVersion, out var rowVersion))
            return ApiResults.FromError(ProductErrors.Validation("Row version must be the base64 value returned by the API."));

        var result = await handler.Handle(
            new UpdateProductCommand(id, request.Name, request.Description, request.Price, request.CategoryId, rowVersion),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.FromError(result.Error);
    }

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPost("{id:guid}/deactivate")]
    public Task<IResult> Deactivate(
        Guid id,
        RowVersionRequest request,
        SetProductActivationHandler handler,
        CancellationToken cancellationToken) =>
        SetActivation(id, isActive: false, request, handler, cancellationToken);

    [Authorize(Policy = ProductPolicies.AdminOnly)]
    [HttpPost("{id:guid}/activate")]
    public Task<IResult> Activate(
        Guid id,
        RowVersionRequest request,
        SetProductActivationHandler handler,
        CancellationToken cancellationToken) =>
        SetActivation(id, isActive: true, request, handler, cancellationToken);

    private static async Task<IResult> SetActivation(
        Guid id,
        bool isActive,
        RowVersionRequest request,
        SetProductActivationHandler handler,
        CancellationToken cancellationToken)
    {
        if (!RowVersionToken.TryDecode(request.RowVersion, out var rowVersion))
            return ApiResults.FromError(ProductErrors.Validation("Row version must be the base64 value returned by the API."));

        var result = await handler.Handle(new SetProductActivationCommand(id, isActive, rowVersion), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ApiResults.FromError(result.Error);
    }

    private static bool TryParseSort(string? sort, out ProductSort parsed)
    {
        parsed = sort?.Trim().ToLowerInvariant() switch
        {
            null or "" or "name" => ProductSort.Name,
            "priceasc" => ProductSort.PriceAscending,
            "pricedesc" => ProductSort.PriceDescending,
            "newest" => ProductSort.Newest,
            _ => ProductSort.Name
        };

        return sort is null
            || sort.Trim().Length == 0
            || sort.Trim().ToLowerInvariant() is "name" or "priceasc" or "pricedesc" or "newest";
    }
}
