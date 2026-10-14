using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>Input of <see cref="BrandCreateRequest"/> (400). A name another brand already has is a 409 of <see cref="BrandService"/>.</summary>
public sealed class BrandCreateRequestValidator : AbstractValidator<BrandCreateRequest>
{
    public const int NameMaxLength = 100;

    public BrandCreateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .Must(n => n.Trim().Length <= NameMaxLength).WithMessage($"Name must be at most {NameMaxLength} characters.")
            // A name without one gives an empty slug.
            .Must(n => CatalogSlug.From(n).Length > 0).WithMessage("Name needs at least one Latin letter or digit.");
    }
}
