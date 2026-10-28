namespace ZenGear.Services.Catalog.Brands;

/// <summary>The brand as the staff edits it (UC-011): every field is sent.</summary>
/// <param name="Name">
/// Stored trimmed; the slug stays as it was created. Nullable so the validator, not model binding, answers a missing one.
/// </param>
/// <param name="IsActive">
/// False retires the brand, true restores it (R-DEL-01). Nullable so that a body without it is refused by the validator
/// instead of retiring the brand.
/// </param>
public sealed record BrandUpdateRequest(string? Name, bool? IsActive);
