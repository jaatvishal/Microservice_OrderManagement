namespace Product.Domain.Catalog;

public sealed class Category
{
    private Category()
    {
    }

    private Category(Guid id, string name, string slug, DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Slug = slug;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Stable public key. Renames do not change it, so storefront URLs survive a display-name edit.
    /// </summary>
    public string Slug { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Category Create(string name, DateTime utcNow)
    {
        var trimmed = name.Trim();
        var slug = CatalogSlug.FromName(trimmed);
        return new Category(Guid.NewGuid(), trimmed, slug, utcNow);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name.Trim();
    }

    public void Deactivate() => IsActive = false;
}
