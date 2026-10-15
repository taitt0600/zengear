namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// The permission keys checked by the endpoints (<c>resource.action</c>, DEC-095). A key is stored in
/// <c>permissions</c>: renaming one later drops the grants the Admin gave it (AUT-05), so treat names as permanent.
/// </summary>
public static class PermissionKeys
{
    /// <summary>Create a staff account and give it roles (STF-01).</summary>
    public const string StaffCreate = "staff.create";

    /// <summary>Read a staff account (STF-01).</summary>
    public const string StaffRead = "staff.read";

    /// <summary>Disable and restore a staff account (STF-02).</summary>
    public const string StaffDisable = "staff.disable";

    /// <summary>Read the edit form of the business location and change it (LOC-01).</summary>
    public const string BusinessLocationUpdate = "business_location.update";

    /// <summary>Read the brands (PRD-02).</summary>
    public const string BrandRead = "brand.read";

    /// <summary>Create a brand (PRD-02).</summary>
    public const string BrandCreate = "brand.create";

    /// <summary>Rename, retire and restore a brand (PRD-02).</summary>
    public const string BrandUpdate = "brand.update";

    /// <summary>Read the product series (PRD-03).</summary>
    public const string ProductSeriesRead = "product_series.read";

    /// <summary>Create a product series (PRD-03).</summary>
    public const string ProductSeriesCreate = "product_series.create";

    /// <summary>Rename, move to another brand, retire and restore a product series (PRD-03).</summary>
    public const string ProductSeriesUpdate = "product_series.update";
}
