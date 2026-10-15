using System.Text.Json.Serialization;

namespace ZenGear.Services.Catalog.Series;

/// <summary>A product series, for the staff screens (PRD-03).</summary>
/// <param name="IsActive">False = retired: kept for history, left out of the lists that create new data (R-DEL-01).</param>
/// <param name="BrandId">The one brand the series belongs to.</param>
/// <param name="Version"><c>product_series.xmin</c>, sent as the <c>ETag</c> header and never in the body.</param>
public sealed record ProductSeriesDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    Guid BrandId,
    [property: JsonIgnore] uint Version);
