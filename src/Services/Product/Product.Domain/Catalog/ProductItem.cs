namespace Product.Domain.Catalog;

/// <summary>
/// A sellable catalog entry. Stock is intentionally absent: quantity belongs to Inventory.
/// SKU and currency are fixed after creation because later services will reference them.
/// </summary>
public sealed class ProductItem
{
    public const int SkuMaxLength = 64;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.Ordinal)
    {
        "USD",
        "EUR",
        "GBP"
    };

    private ProductItem()
    {
    }

    private ProductItem(
        Guid id,
        string sku,
        string name,
        string? description,
        decimal price,
        string currency,
        Guid categoryId,
        DateTime utcNow)
    {
        Id = id;
        Sku = sku;
        Name = name;
        Description = description;
        Price = price;
        Currency = currency;
        CategoryId = categoryId;
        IsActive = true;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public Guid CategoryId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static ProductItem Create(
        string sku,
        string name,
        string? description,
        decimal price,
        string currency,
        Guid categoryId,
        DateTime utcNow)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category is required.", nameof(categoryId));

        return new ProductItem(
            Guid.NewGuid(),
            NormalizeRequiredSku(sku),
            NormalizeName(name),
            NormalizeDescription(description),
            NormalizePrice(price),
            NormalizeCurrency(currency),
            categoryId,
            utcNow);
    }

    public void UpdateDetails(string name, string? description, decimal price, Guid categoryId, DateTime utcNow)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category is required.", nameof(categoryId));

        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        Price = NormalizePrice(price);
        CategoryId = categoryId;
        UpdatedAtUtc = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        UpdatedAtUtc = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        UpdatedAtUtc = utcNow;
    }

    internal void StampRowVersion(byte[] rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0)
            throw new ArgumentException("Row version is required.", nameof(rowVersion));

        RowVersion = rowVersion;
    }

    private static string NormalizeRequiredSku(string sku)
    {
        var normalized = NormalizeSku(sku);
        if (normalized.Length is 0 or > SkuMaxLength)
            throw new ArgumentException("SKU is required and must be 64 characters or fewer.", nameof(sku));

        foreach (var character in normalized)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                throw new ArgumentException("SKU may contain only letters, digits, and hyphens.", nameof(sku));
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new ArgumentException("Name must be 200 characters or fewer.", nameof(name));

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var trimmed = description.Trim();
        if (trimmed.Length > DescriptionMaxLength)
            throw new ArgumentException("Description must be 4000 characters or fewer.", nameof(description));

        return trimmed;
    }

    private static decimal NormalizePrice(decimal price)
    {
        if (price < 0 || price > 1_000_000m)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be between 0 and 1000000.");

        if (price != decimal.Round(price, 2, MidpointRounding.AwayFromZero))
            throw new ArgumentException("Price supports two decimal places.", nameof(price));

        return price;
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(normalized))
            throw new ArgumentException("Currency must be USD, EUR, or GBP.", nameof(currency));

        return normalized;
    }
}
