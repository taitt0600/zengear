namespace ZenGear.Data.Entities.Geography;

/// <summary>
/// A commune, ward or special zone (<c>wards</c>), loaded from the official list (Quyết định 19/2025/QĐ-TTg); the
/// application only reads it.
/// </summary>
public class Ward
{
    protected Ward()
    {
    }

    /// <summary>The official code, not generated.</summary>
    public int Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Code { get; private set; }

    public string? Type { get; private set; }

    public string? ZipCode { get; private set; }

    public string? PhoneCode { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Nullable to match the seed source (<c>ward &gt;? province</c>).</summary>
    public int? ProvinceId { get; private set; }
}
