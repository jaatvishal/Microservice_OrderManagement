using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Domain.Catalog;

namespace Product.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Slug).HasMaxLength(120).IsRequired();
        builder.Property(category => category.CreatedAtUtc).IsRequired();

        // Storefront menus and create-category both look up the stable slug.
        builder.HasIndex(category => category.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Categories_Slug");

        // Public category list is active categories ordered by name.
        builder.HasIndex(category => new { category.IsActive, category.Name })
            .HasDatabaseName("IX_Categories_IsActive_Name");
    }
}
