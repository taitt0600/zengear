using ZenGear.Data.Common;

namespace ZenGear.Data.Entities.Catalog;

/// <summary>
/// A brand (<c>brands</c>, PRD-02). Never deleted: it is retired with <see cref="IsActive"/> and stays in the history of
/// what references it (R-DEL-01, DEC-022).
/// </summary>
public class Brand : IAggregateRoot
{
    protected Brand()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>
    /// Set from the name at creation and kept on rename: the seed (<c>api/seed/07_categories_brands.sql</c>) runs again at
    /// every deploy and finds the brands it already inserted by slug.
    /// </summary>
    public string Slug { get; private set; } = null!;

    public string? LogoUrl { get; private set; }

    /// <summary>False = retired: kept for history, left out of the lists that create new data (R-DEL-01).</summary>
    public bool IsActive { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public uint Version { get; private set; }

    /// <summary>An active brand (UC-010). The service validates first for a clear error; these checks are the backstop.</summary>
    /// <exception cref="ArgumentException">A blank name or slug.</exception>
    public static Brand Create(string name, string slug, Guid createdBy, DateTimeOffset now) => new()
    {
        Name = Required(name, nameof(name)),
        Slug = Required(slug, nameof(slug)),
        IsActive = true,
        CreatedBy = createdBy,
        CreatedAt = now,
        UpdatedBy = createdBy,
        UpdatedAt = now,
    };

    /// <summary>
    /// Renames, retires or restores the brand (UC-011); the slug stays. Stamps the change only when something differs.
    /// </summary>
    /// <exception cref="ArgumentException">A blank name.</exception>
    public void Update(string name, bool isActive, Guid updatedBy, DateTimeOffset now)
    {
        var trimmed = Required(name, nameof(name));
        if (trimmed == Name && isActive == IsActive)
        {
            return;
        }

        Name = trimmed;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    private static string Required(string? value, string paramName)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? throw new ArgumentException("A required value is blank.", paramName) : trimmed;
    }
}
