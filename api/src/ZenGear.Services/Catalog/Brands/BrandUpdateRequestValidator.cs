using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>Input of <see cref="BrandUpdateRequest"/> (400). A name another brand already has is a 409 of <see cref="BrandService"/>.</summary>
public sealed class BrandUpdateRequestValidator : AbstractValidator<BrandUpdateRequest>
{
    /// <summary>A catalog record sent without saying whether it stays in use; the series use it too.</summary>
    public const string IsActiveRequired = "IS_ACTIVE_REQUIRED";

    public BrandUpdateRequestValidator()
    {
        // The slug stays as created, but the name must still give one to be compared with the other brands.
        RuleFor(r => r.Name!).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(CatalogNameValidator.NameRequired)
            .SetValidator(new CatalogNameValidator());

        RuleFor(r => r.IsActive).NotNull().WithErrorCode(IsActiveRequired);
    }
}
