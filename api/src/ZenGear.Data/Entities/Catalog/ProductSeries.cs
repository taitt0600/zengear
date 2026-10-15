using ZenGear.Data.Common;

namespace ZenGear.Data.Entities.Catalog;

/// <summary>
/// A product series (<c>product_series</c>, PRD-03): a line of products of exactly one brand. Never deleted: it is
/// retired with <see cref="IsActive"/> and stays in the history of what references it (R-DEL-01, DEC-022).
/// </summary>
public class ProductSeries : IAggregateRoot
{
    protected ProductSeries()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Set from the name at creation and kept on rename, as the slug of a brand.</summary>
    public string Slug { get; private set; } = null!;

    /// <summary>False = retired: kept for history, left out of the lists that create new data (R-DEL-01).</summary>
    public bool IsActive { get; private set; }

    public Guid BrandId { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public uint Version { get; private set; }

    /// <summary>An active series of the brand <paramref name="brandId"/> (UC-012). The service validates first; these checks are the backstop.</summary>
    /// <exception cref="ArgumentException">A blank name or slug, or an empty brand id.</exception>
    public static ProductSeries Create(string name, string slug, Guid brandId, Guid createdBy, DateTimeOffset now) => new()
    {
        Name = Required(name, nameof(name)),
        Slug = Required(slug, nameof(slug)),
        BrandId = RequiredBrand(brandId),
        IsActive = true,
        CreatedBy = createdBy,
        CreatedAt = now,
        UpdatedBy = createdBy,
        UpdatedAt = now,
    };

    /// <summary>
    /// Renames the series, moves it to another brand, retires or restores it (UC-013); the slug stays. Stamps the
    /// change only when something differs. Whether its products allow the move is checked with them (R-PRD-07, PRD-05).
    /// </summary>
    /// <exception cref="ArgumentException">A blank name or an empty brand id.</exception>
    public void Update(string name, Guid brandId, bool isActive, Guid updatedBy, DateTimeOffset now)
    {
        var trimmed = Required(name, nameof(name));
        RequiredBrand(brandId);
        if (trimmed == Name && brandId == BrandId && isActive == IsActive)
        {
            return;
        }

        Name = trimmed;
        BrandId = brandId;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    private static string Required(string? value, string paramName)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? throw new ArgumentException("A required value is blank.", paramName) : trimmed;
    }

    private static Guid RequiredBrand(Guid brandId) =>
        brandId == Guid.Empty ? throw new ArgumentException("A series belongs to exactly one brand.", nameof(brandId)) : brandId;
}
