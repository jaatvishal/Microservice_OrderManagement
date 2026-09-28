using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using Product.Domain.Catalog;

namespace Product.Application.Catalog;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId);

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(ProductItem.SkuMaxLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(ProductItem.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(ProductItem.DescriptionMaxLength);
        RuleFor(command => command.Price).InclusiveBetween(0, 1_000_000m);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
        RuleFor(command => command.CategoryId).NotEmpty();
    }
}

public sealed class CreateProductHandler(
    ICatalogRepository catalog,
    IDateTimeProvider clock,
    IValidator<CreateProductCommand> validator,
    ILogger<CreateProductHandler> logger)
{
    public async Task<Result<ProductDetails>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<ProductDetails>(ProductErrors.Validation(Join(validation)));

        var category = await catalog.GetCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure<ProductDetails>(ProductErrors.NotFound("Category was not found."));
        if (!category.IsActive)
            return Result.Failure<ProductDetails>(ProductErrors.CategoryUnavailable());

        var normalizedSku = ProductItem.NormalizeSku(command.Sku);
        if (await catalog.GetProductBySkuAsync(normalizedSku, cancellationToken) is not null)
            return Result.Failure<ProductDetails>(ProductErrors.SkuConflict());

        var product = ProductItem.Create(
            command.Sku,
            command.Name,
            command.Description,
            command.Price,
            command.Currency,
            command.CategoryId,
            clock.UtcNow);

        await catalog.AddProductAsync(product, cancellationToken);
        try
        {
            await catalog.SaveChangesAsync(cancellationToken);
        }
        catch (SkuAlreadyExistsException)
        {
            logger.LogInformation("Product SKU {Sku} lost a uniqueness race", product.Sku);
            return Result.Failure<ProductDetails>(ProductErrors.SkuConflict());
        }

        logger.LogInformation("Created product {ProductId} with SKU {Sku}", product.Id, product.Sku);
        return Result.Success(ToDetails(product, category));
    }

    public static ProductDetails ToDetails(ProductItem product, Category category) =>
        new(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.Price,
            product.Currency,
            product.CategoryId,
            category.Name,
            product.IsActive,
            RowVersionToken.Encode(product.RowVersion),
            product.UpdatedAtUtc);

    private static string Join(FluentValidation.Results.ValidationResult validation) =>
        string.Join(' ', validation.Errors.Select(error => error.ErrorMessage));
}

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    byte[] RowVersion);

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(ProductItem.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(ProductItem.DescriptionMaxLength);
        RuleFor(command => command.Price).InclusiveBetween(0, 1_000_000m);
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.RowVersion).NotEmpty();
    }
}

public sealed class UpdateProductHandler(
    ICatalogRepository catalog,
    IDateTimeProvider clock,
    IValidator<UpdateProductCommand> validator,
    ILogger<UpdateProductHandler> logger)
{
    public async Task<Result<ProductDetails>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<ProductDetails>(ProductErrors.Validation(string.Join(' ', validation.Errors.Select(error => error.ErrorMessage))));

        var product = await catalog.GetProductForUpdateAsync(command.ProductId, cancellationToken);
        if (product is null)
            return Result.Failure<ProductDetails>(ProductErrors.NotFound("Product was not found."));

        if (!product.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
            return Result.Failure<ProductDetails>(ProductErrors.Concurrency());

        var category = await catalog.GetCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure<ProductDetails>(ProductErrors.NotFound("Category was not found."));
        if (!category.IsActive && category.Id != product.CategoryId)
            return Result.Failure<ProductDetails>(ProductErrors.CategoryUnavailable());

        product.UpdateDetails(command.Name, command.Description, command.Price, command.CategoryId, clock.UtcNow);

        try
        {
            await catalog.SaveChangesAsync(cancellationToken);
        }
        catch (ProductConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concurrent update for product {ProductId}", product.Id);
            return Result.Failure<ProductDetails>(ProductErrors.Concurrency());
        }

        logger.LogInformation("Updated product {ProductId}", product.Id);
        return Result.Success(CreateProductHandler.ToDetails(product, category));
    }
}

public sealed record SetProductActivationCommand(Guid ProductId, bool IsActive, byte[] RowVersion);

public sealed class SetProductActivationHandler(
    ICatalogRepository catalog,
    IDateTimeProvider clock,
    ILogger<SetProductActivationHandler> logger)
{
    public async Task<Result> Handle(SetProductActivationCommand command, CancellationToken cancellationToken)
    {
        if (command.RowVersion.Length == 0)
            return Result.Failure(ProductErrors.Validation("Row version is required."));

        var product = await catalog.GetProductForUpdateAsync(command.ProductId, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.NotFound("Product was not found."));

        if (!product.RowVersion.AsSpan().SequenceEqual(command.RowVersion))
            return Result.Failure(ProductErrors.Concurrency());

        if (product.IsActive == command.IsActive)
            return Result.Success();

        if (command.IsActive)
            product.Activate(clock.UtcNow);
        else
            product.Deactivate(clock.UtcNow);

        try
        {
            await catalog.SaveChangesAsync(cancellationToken);
        }
        catch (ProductConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concurrent activation change for product {ProductId}", product.Id);
            return Result.Failure(ProductErrors.Concurrency());
        }

        logger.LogInformation("Set product {ProductId} active flag to {IsActive}", product.Id, product.IsActive);
        return Result.Success();
    }
}

public sealed record GetProductQuery(Guid ProductId, bool IncludeInactive);

public sealed class GetProductHandler(ICatalogRepository catalog)
{
    public async Task<Result<ProductDetails>> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        var product = await catalog.GetProductDetailsAsync(query.ProductId, cancellationToken);
        if (product is null || (!product.IsActive && !query.IncludeInactive))
            return Result.Failure<ProductDetails>(ProductErrors.NotFound("Product was not found."));

        return Result.Success(product);
    }
}

public sealed record ListProductsQuery(ProductSearch Search);

public sealed class ListProductsHandler(ICatalogRepository catalog)
{
    public async Task<Result<PagedList<ProductSummary>>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var search = query.Search;
        if (search.Page < 1 || search.PageSize is < 1 or > 50)
            return Result.Failure<PagedList<ProductSummary>>(ProductErrors.Validation("Page must be at least 1 and page size must be between 1 and 50."));

        if (search.Search is { Length: > 100 })
            return Result.Failure<PagedList<ProductSummary>>(ProductErrors.Validation("Search text must be 100 characters or fewer."));

        var page = await catalog.SearchProductsAsync(search, cancellationToken);
        return Result.Success(page);
    }
}
