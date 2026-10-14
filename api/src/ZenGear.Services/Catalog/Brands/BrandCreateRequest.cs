namespace ZenGear.Services.Catalog.Brands;

/// <summary>A new brand (UC-010).</summary>
/// <param name="Name">Stored trimmed; its slug must not belong to another brand.</param>
public sealed record BrandCreateRequest(string Name);
