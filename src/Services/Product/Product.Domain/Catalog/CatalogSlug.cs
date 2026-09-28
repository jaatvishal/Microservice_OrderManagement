using System.Text;

namespace Product.Domain.Catalog;

public static class CatalogSlug
{
    public static string FromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var builder = new StringBuilder(name.Length);
        var pendingHyphen = false;

        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingHyphen && builder.Length > 0)
                    builder.Append('-');

                pendingHyphen = false;
                builder.Append(character);
            }
            else
            {
                pendingHyphen = true;
            }
        }

        if (builder.Length == 0)
            throw new ArgumentException("Name must contain a letter or digit.", nameof(name));

        return builder.ToString();
    }
}
