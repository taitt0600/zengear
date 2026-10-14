using System.Text.Json.Serialization;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>A brand, for the staff screens (PRD-02).</summary>
/// <param name="IsActive">False = retired: kept for history, left out of the lists that create new data (R-DEL-01).</param>
/// <param name="Version"><c>brands.xmin</c>, sent as the <c>ETag</c> header and never in the body.</param>
public sealed record BrandDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    [property: JsonIgnore] uint Version);
