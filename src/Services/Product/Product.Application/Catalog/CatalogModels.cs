namespace Product.Application.Catalog;

public enum ProductSort
{
    Name = 0,
    PriceAscending = 1,
    PriceDescending = 2,
    Newest = 3
}

public sealed record CategorySummary(Guid Id, string Name, string Slug, bool IsActive);

public sealed record ProductSummary(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string CategoryName,
    bool IsActive);

public sealed record ProductDetails(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string CategoryName,
    bool IsActive,
    string RowVersion,
    DateTime UpdatedAtUtc);

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ProductSearch(
    int Page,
    int PageSize,
    Guid? CategoryId,
    string? Search,
    ProductSort Sort,
    bool ActiveOnly);
