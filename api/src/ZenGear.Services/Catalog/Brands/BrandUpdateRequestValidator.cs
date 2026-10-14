using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>Input of <see cref="BrandUpdateRequest"/> (400). A name another brand already has is a 409 of <see cref="BrandService"/>.</summary>
public sealed class BrandUpdateRequestValidator : AbstractValidator<BrandUpdateRequest>
{
    public BrandUpdateRequestValidator()
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .Must(n => n.Trim().Length <= BrandCreateRequestValidator.NameMaxLength)
            .WithMessage($"Name must be at most {BrandCreateRequestValidator.NameMaxLength} characters.")
            // The slug stays as created, but the name must still give one to be compared with the other brands.
            .Must(n => CatalogSlug.From(n).Length > 0).WithMessage("Name needs at least one Latin letter or digit.");

        RuleFor(r => r.IsActive).NotNull().WithMessage("IsActive is required: true keeps the brand in use, false retires it.");
    }
}
