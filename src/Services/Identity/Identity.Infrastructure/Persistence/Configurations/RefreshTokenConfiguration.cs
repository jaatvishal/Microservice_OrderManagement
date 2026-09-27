using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(token => token.CreatedByIp).HasMaxLength(64);
        builder.Property(token => token.CreatedAtUtc).IsRequired();
        builder.Property(token => token.ExpiresAtUtc).IsRequired();

        // Optimistic concurrency for rotation. Two refreshes of the same token cannot both commit.
        builder.Property(token => token.RowVersion).IsRowVersion();

        // Refresh is a point lookup by hash. Uniqueness also rejects a duplicated secret.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        // Revoke-all and loading a user's sessions filter by user id.
        builder.HasIndex(token => token.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");
    }
}
