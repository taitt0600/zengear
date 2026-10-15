using FluentValidation;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Catalog.Series;

/// <summary>
/// Input of <see cref="ProductSeriesCreateRequest"/> (400). An unknown brand is a 400 and a retired brand or a name
/// another series already has a 409, of <see cref="ProductSeriesService"/>.
/// </summary>
public sealed class ProductSeriesCreateRequestValidator : AbstractValidator<ProductSeriesCreateRequest>
{
    public ProductSeriesCreateRequestValidator()
    {
        // A child validator skips a null value, so the null check stays here.
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Name is required.")
            .SetValidator(new CatalogNameValidator());

        // NotEmpty lets Guid.Empty through on a nullable Guid.
        RuleFor(r => r.BrandId).Must(id => id is { } brand && brand != Guid.Empty).WithMessage("BrandId is required: a series belongs to exactly one brand.");
    }
}
