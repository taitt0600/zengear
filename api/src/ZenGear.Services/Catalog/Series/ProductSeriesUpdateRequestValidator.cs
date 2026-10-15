using FluentValidation;
using ZenGear.Services.Catalog.Brands;

namespace ZenGear.Services.Catalog.Series;

/// <summary>
/// Input of <see cref="ProductSeriesUpdateRequest"/> (400). An unknown brand is a 400 and a retired new brand or a name
/// another series already has a 409, of <see cref="ProductSeriesService"/>.
/// </summary>
public sealed class ProductSeriesUpdateRequestValidator : AbstractValidator<ProductSeriesUpdateRequest>
{
    public ProductSeriesUpdateRequestValidator()
    {
        // The slug stays as created, but the name must still give one to be compared with the other series.
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Name is required.")
            .SetValidator(new CatalogNameValidator());

        // NotEmpty lets Guid.Empty through on a nullable Guid.
        RuleFor(r => r.BrandId).Must(id => id is { } brand && brand != Guid.Empty).WithMessage("BrandId is required: a series belongs to exactly one brand.");

        RuleFor(r => r.IsActive).NotNull().WithMessage("IsActive is required: true keeps the series in use, false retires it.");
    }
}
