namespace ZenGear.Services.Catalog.Series;

/// <summary>The series as the staff edits it (UC-013): every field is sent.</summary>
/// <param name="Name">Stored trimmed; the slug stays as it was created.</param>
/// <param name="BrandId">The brand it belongs to; another brand must be active (R-DEL-01).</param>
/// <param name="IsActive">
/// False retires the series, true restores it (R-DEL-01). Nullable so that a body without it is refused by the validator
/// instead of retiring the series.
/// </param>
public sealed record ProductSeriesUpdateRequest(string Name, Guid? BrandId, bool? IsActive);
