using BuildingBlocks.Application.Models;

namespace Product.Application.Catalog;

public static class ProductErrors
{
    public static Error Validation(string message) => Error.Validation("Product.Validation", message);

    public static Error NotFound(string message) => Error.NotFound("Product.NotFound", message);

    public static Error SkuConflict() =>
        Error.Conflict("Product.SkuConflict", "A product with this SKU already exists.");

    public static Error SlugConflict() =>
        Error.Conflict("Product.SlugConflict", "A category with this name already exists.");

    public static Error Concurrency() =>
        Error.Conflict("Product.Concurrency", "The product was updated by someone else. Reload and try again.");

    public static Error CategoryUnavailable() =>
        Error.Conflict("Product.CategoryUnavailable", "Category is not available for new products.");
}
