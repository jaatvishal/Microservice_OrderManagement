namespace Product.Application.Catalog;

public sealed class SkuAlreadyExistsException : Exception
{
    public SkuAlreadyExistsException(Exception innerException)
        : base("A product with this SKU already exists.", innerException)
    {
    }
}

public sealed class SlugAlreadyExistsException : Exception
{
    public SlugAlreadyExistsException(Exception innerException)
        : base("A category with this name already exists.", innerException)
    {
    }
}

public sealed class ProductConcurrencyException : Exception
{
    public ProductConcurrencyException(Exception innerException)
        : base("The product was updated by someone else.", innerException)
    {
    }
}
