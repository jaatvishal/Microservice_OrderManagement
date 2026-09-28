namespace Product.API.Contracts;

public sealed record CreateCategoryRequest(string Name);

public sealed record RenameCategoryRequest(string Name);

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    string RowVersion);

public sealed record RowVersionRequest(string RowVersion);
