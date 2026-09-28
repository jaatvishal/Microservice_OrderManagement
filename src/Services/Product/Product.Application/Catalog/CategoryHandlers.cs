using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using Product.Domain.Catalog;

namespace Product.Application.Catalog;

public sealed record CreateCategoryCommand(string Name);

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100)
            .Must(name => name.Any(char.IsLetterOrDigit))
            .WithMessage("Category name must contain a letter or digit.");
    }
}

public sealed class CreateCategoryHandler(
    ICatalogRepository catalog,
    IDateTimeProvider clock,
    IValidator<CreateCategoryCommand> validator,
    ILogger<CreateCategoryHandler> logger)
{
    public async Task<Result<CategorySummary>> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<CategorySummary>(ProductErrors.Validation(Join(validation)));

        var category = Category.Create(command.Name, clock.UtcNow);
        var existing = await catalog.GetCategoryBySlugAsync(category.Slug, cancellationToken);
        if (existing is not null)
            return Result.Failure<CategorySummary>(ProductErrors.SlugConflict());

        await catalog.AddCategoryAsync(category, cancellationToken);
        try
        {
            await catalog.SaveChangesAsync(cancellationToken);
        }
        catch (SlugAlreadyExistsException)
        {
            return Result.Failure<CategorySummary>(ProductErrors.SlugConflict());
        }

        logger.LogInformation("Created category {CategoryId}", category.Id);
        return Result.Success(new CategorySummary(category.Id, category.Name, category.Slug, category.IsActive));
    }

    private static string Join(FluentValidation.Results.ValidationResult validation) =>
        string.Join(' ', validation.Errors.Select(error => error.ErrorMessage));
}

public sealed record RenameCategoryCommand(Guid CategoryId, string Name);

public sealed class RenameCategoryValidator : AbstractValidator<RenameCategoryCommand>
{
    public RenameCategoryValidator()
    {
        RuleFor(command => command.CategoryId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class RenameCategoryHandler(
    ICatalogRepository catalog,
    IValidator<RenameCategoryCommand> validator,
    ILogger<RenameCategoryHandler> logger)
{
    public async Task<Result<CategorySummary>> Handle(RenameCategoryCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<CategorySummary>(ProductErrors.Validation(string.Join(' ', validation.Errors.Select(error => error.ErrorMessage))));

        var category = await catalog.GetCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure<CategorySummary>(ProductErrors.NotFound("Category was not found."));

        category.Rename(command.Name);
        await catalog.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Renamed category {CategoryId}", category.Id);
        return Result.Success(new CategorySummary(category.Id, category.Name, category.Slug, category.IsActive));
    }
}

public sealed record ListCategoriesQuery(int Page, int PageSize, bool ActiveOnly);

public sealed class ListCategoriesHandler(ICatalogRepository catalog)
{
    public async Task<Result<PagedList<CategorySummary>>> Handle(ListCategoriesQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            return Result.Failure<PagedList<CategorySummary>>(ProductErrors.Validation("Page must be at least 1 and page size must be between 1 and 100."));

        var page = await catalog.ListCategoriesAsync(query.Page, query.PageSize, query.ActiveOnly, cancellationToken);
        return Result.Success(page);
    }
}

public sealed record DeactivateCategoryCommand(Guid CategoryId);

public sealed class DeactivateCategoryHandler(ICatalogRepository catalog, ILogger<DeactivateCategoryHandler> logger)
{
    public async Task<Result> Handle(DeactivateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await catalog.GetCategoryAsync(command.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure(ProductErrors.NotFound("Category was not found."));

        category.Deactivate();
        await catalog.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deactivated category {CategoryId}", category.Id);
        return Result.Success();
    }
}
