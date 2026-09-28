using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Domain.Catalog;

namespace Product.Infrastructure.Persistence.Configurations;

public sealed class ProductItemConfiguration : IEntityTypeConfiguration<ProductItem>
{
    public void Configure(EntityTypeBuilder<ProductItem> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price_NonNegative", "[Price] >= 0");
        });

        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();
        builder.Property(product => product.Sku).HasMaxLength(ProductItem.SkuMaxLength).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(ProductItem.NameMaxLength).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(ProductItem.DescriptionMaxLength);
        builder.Property(product => product.Price).HasPrecision(18, 2);
        builder.Property(product => product.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(product => product.CreatedAtUtc).IsRequired();
        builder.Property(product => product.UpdatedAtUtc).IsRequired();
        builder.Property(product => product.RowVersion).IsRowVersion();

        // SKU is the business key Inventory and Order will store. It must not collide.
        builder.HasIndex(product => product.Sku)
            .IsUnique()
            .HasDatabaseName("IX_Products_Sku");

        // Category pages: WHERE CategoryId = @id AND IsActive = 1 ORDER BY Name.
        builder.HasIndex(product => new { product.CategoryId, product.IsActive, product.Name })
            .HasDatabaseName("IX_Products_CategoryId_IsActive_Name");

        // Unfiltered catalog: WHERE IsActive = 1 ORDER BY Name.
        builder.HasIndex(product => new { product.IsActive, product.Name, product.Id })
            .HasDatabaseName("IX_Products_IsActive_Name_Id");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
