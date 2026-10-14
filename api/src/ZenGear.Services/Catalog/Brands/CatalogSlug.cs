using System.Globalization;
using System.Text;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>
/// The slug of a catalog name, by the algorithm of R-PRD-06: lowercase, Vietnamese marks removed, words joined by
/// hyphens. It gives the slugs of <c>api/seed/07_categories_brands.sql</c>.
/// </summary>
public static class CatalogSlug
{
    /// <summary>The slug of <paramref name="name"/>; empty when it has no Latin letter or digit.</summary>
    public static string From(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        // NFD splits the marks off every Vietnamese letter except đ, which has no decomposition.
        var decomposed = name.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(decomposed.Length);
        var pendingHyphen = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingHyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(c);
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return slug.ToString();
    }
}
