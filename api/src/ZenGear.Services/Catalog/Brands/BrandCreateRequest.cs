namespace ZenGear.Services.Catalog.Brands;

/// <summary>A new brand (UC-010).</summary>
/// <param name="Name">
/// Stored trimmed; its slug must not belong to another brand. Nullable so the validator, not model binding, answers a
/// missing one.
/// </param>
public sealed record BrandCreateRequest(string? Name);
