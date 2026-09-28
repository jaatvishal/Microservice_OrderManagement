using Product.Application.Catalog;
using Product.Domain.Catalog;
using Product.UnitTests.Support;

namespace Product.UnitTests;

public class CatalogDomainTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Slug_is_stable_and_lowercase()
    {
        Assert.Equal("distributed-systems", CatalogSlug.FromName("  Distributed Systems! "));
    }

    [Fact]
    public void Rename_does_not_change_the_slug()
    {
        var category = Category.Create("Books", Now);
        category.Rename("Literature");

        Assert.Equal("Literature", category.Name);
        Assert.Equal("books", category.Slug);
    }

    [Fact]
    public void Product_normalizes_sku_and_rejects_a_negative_price()
    {
        var categoryId = Guid.NewGuid();
        var product = ProductItem.Create(" book-001 ", "Primer", null, 42m, "usd", categoryId, Now);

        Assert.Equal("BOOK-001", product.Sku);
        Assert.Equal("USD", product.Currency);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProductItem.Create("BOOK-002", "Primer", null, -1m, "USD", categoryId, Now));
    }

    [Fact]
    public void Product_rejects_more_than_two_decimal_places()
    {
        var act = () => ProductItem.Create("BOOK-002", "Primer", null, 1.999m, "USD", Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Update_does_not_change_sku_or_currency()
    {
        var categoryId = Guid.NewGuid();
        var product = ProductItem.Create("BOOK-001", "Primer", null, 42m, "USD", categoryId, Now);

        product.UpdateDetails("Revised", "A longer text", 40m, Guid.NewGuid(), Now.AddMinutes(1));

        Assert.Equal("BOOK-001", product.Sku);
        Assert.Equal("USD", product.Currency);
        Assert.Equal(40m, product.Price);
        Assert.Equal(Now.AddMinutes(1), product.UpdatedAtUtc);
    }
}

public class CatalogHandlerTests
{
    private readonly FixedClock _clock = new();
    private readonly InMemoryCatalogRepository _catalog = new();

    [Fact]
    public async Task Create_product_hides_it_after_deactivation_unless_inactive_records_are_requested()
    {
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id, "book-001");
        var version = RowVersionToken.TryDecode(created.Value.RowVersion, out var bytes) ? bytes : [];

        var deactivate = await new SetProductActivationHandler(_catalog, _clock, NullLog.For<SetProductActivationHandler>())
            .Handle(new SetProductActivationCommand(created.Value.Id, false, version), CancellationToken.None);

        var hidden = await new GetProductHandler(_catalog).Handle(new GetProductQuery(created.Value.Id, false), CancellationToken.None);
        var visibleToAdmin = await new GetProductHandler(_catalog).Handle(new GetProductQuery(created.Value.Id, true), CancellationToken.None);
        var list = await new ListProductsHandler(_catalog).Handle(
            new ListProductsQuery(new ProductSearch(1, 20, null, null, ProductSort.Name, true)),
            CancellationToken.None);

        Assert.True(deactivate.IsSuccess);
        Assert.Equal("Product.NotFound", hidden.Error.Code);
        Assert.True(visibleToAdmin.IsSuccess);
        Assert.Empty(list.Value.Items);
    }

    [Fact]
    public async Task Duplicate_sku_is_a_conflict_and_a_uniqueness_race_is_a_conflict()
    {
        var category = await CreateCategoryAsync();
        await CreateProductAsync(category.Id, "book-001");

        var duplicate = await CreateProductHandler().Handle(
            new CreateProductCommand("BOOK-001", "Other", null, 10m, "USD", category.Id),
            CancellationToken.None);

        _catalog.SaveException = new SkuAlreadyExistsException(new InvalidOperationException("duplicate"));
        var raced = await CreateProductHandler().Handle(
            new CreateProductCommand("book-002", "Other", null, 10m, "USD", category.Id),
            CancellationToken.None);

        Assert.Equal("Product.SkuConflict", duplicate.Error.Code);
        Assert.Equal("Product.SkuConflict", raced.Error.Code);
    }

    [Fact]
    public async Task Stale_row_version_does_not_change_the_price()
    {
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id, "book-001");

        var update = await new UpdateProductHandler(_catalog, _clock, new UpdateProductValidator(), NullLog.For<UpdateProductHandler>())
            .Handle(
                new UpdateProductCommand(created.Value.Id, "Renamed", null, 99m, category.Id, [9, 9, 9, 9, 9, 9, 9, 9]),
                CancellationToken.None);

        var current = await new GetProductHandler(_catalog).Handle(new GetProductQuery(created.Value.Id, true), CancellationToken.None);

        Assert.Equal("Product.Concurrency", update.Error.Code);
        Assert.Equal(42m, current.Value.Price);
    }

    [Fact]
    public async Task Inactive_category_cannot_receive_a_new_product()
    {
        var category = await CreateCategoryAsync();
        await new DeactivateCategoryHandler(_catalog, NullLog.For<DeactivateCategoryHandler>())
            .Handle(new DeactivateCategoryCommand(category.Id), CancellationToken.None);

        var created = await CreateProductHandler().Handle(
            new CreateProductCommand("book-001", "Primer", null, 42m, "USD", category.Id),
            CancellationToken.None);

        Assert.Equal("Product.CategoryUnavailable", created.Error.Code);
    }

    [Fact]
    public async Task List_rejects_an_unbounded_page()
    {
        var result = await new ListProductsHandler(_catalog).Handle(
            new ListProductsQuery(new ProductSearch(1, 500, null, null, ProductSort.Name, true)),
            CancellationToken.None);

        Assert.Equal("Product.Validation", result.Error.Code);
    }

    private async Task<CategorySummary> CreateCategoryAsync()
    {
        var result = await new CreateCategoryHandler(_catalog, _clock, new CreateCategoryValidator(), NullLog.For<CreateCategoryHandler>())
            .Handle(new CreateCategoryCommand("Books"), CancellationToken.None);
        return result.Value;
    }

    private async Task<BuildingBlocks.Application.Models.Result<ProductDetails>> CreateProductAsync(Guid categoryId, string sku)
    {
        return await CreateProductHandler().Handle(
            new CreateProductCommand(sku, "Primer", null, 42m, "USD", categoryId),
            CancellationToken.None);
    }

    private CreateProductHandler CreateProductHandler() =>
        new(_catalog, _clock, new CreateProductValidator(), NullLog.For<CreateProductHandler>());
}
