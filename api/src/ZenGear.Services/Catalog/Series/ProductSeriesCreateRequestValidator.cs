using FluentValidation;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Catalog.Series;

/// <summary>
/// Input of <see cref="ProductSeriesCreateRequest"/> (400). An unknown brand is a 400 and a retired brand or a name
/// another series already has a 409, of <see cref="ProductSeriesService"/>.
/// </summary>
public sealed class ProductSeriesCreateRequestValidator : AbstractValidator<ProductSeriesCreateRequest>
{
    public const string BrandIdRequired = "BRAND_ID_REQUIRED";

    public ProductSeriesCreateRequestValidator()
    {
        // A child validator skips a null value, so the null check stays here.
        RuleFor(r => r.Name!).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(CatalogNameValidator.NameRequired)
            .SetValidator(new CatalogNameValidator());

        // NotEmpty lets Guid.Empty through on a nullable Guid.
        RuleFor(r => r.BrandId).Must(id => id is { } brand && brand != Guid.Empty).WithErrorCode(BrandIdRequired);
    }
}
