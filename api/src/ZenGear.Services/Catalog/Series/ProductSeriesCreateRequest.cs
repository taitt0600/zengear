namespace ZenGear.Services.Catalog.Series;

/// <summary>A new series of one brand (UC-012).</summary>
/// <param name="Name">Stored trimmed; its slug must not belong to another series of any brand.</param>
/// <param name="BrandId">An active brand. Nullable so that a body without it is refused by the validator.</param>
public sealed record ProductSeriesCreateRequest(string Name, Guid? BrandId);
