using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>Input of <see cref="BrandCreateRequest"/> (400). A name another brand already has is a 409 of <see cref="BrandService"/>.</summary>
public sealed class BrandCreateRequestValidator : AbstractValidator<BrandCreateRequest>
{
    public BrandCreateRequestValidator()
    {
        // A child validator skips a null value, so the null check stays here.
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Name is required.")
            .SetValidator(new CatalogNameValidator());
    }
}
