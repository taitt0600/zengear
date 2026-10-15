using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>Input of <see cref="BrandUpdateRequest"/> (400). A name another brand already has is a 409 of <see cref="BrandService"/>.</summary>
public sealed class BrandUpdateRequestValidator : AbstractValidator<BrandUpdateRequest>
{
    public BrandUpdateRequestValidator()
    {
        // The slug stays as created, but the name must still give one to be compared with the other brands.
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Name is required.")
            .SetValidator(new CatalogNameValidator());

        RuleFor(r => r.IsActive).NotNull().WithMessage("IsActive is required: true keeps the brand in use, false retires it.");
    }
}
