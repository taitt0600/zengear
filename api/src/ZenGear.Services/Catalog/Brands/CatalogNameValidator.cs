using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>
/// The name of a catalog record whose slug comes from it (brand, series): set on a request's name with
/// <c>SetValidator</c>, so the error stays on that field. Another record already holding the name is a 409 of the service.
/// Internal so that the assembly scan of the validators does not register it as the <c>IValidator&lt;string&gt;</c> of
/// every string.
/// </summary>
internal sealed class CatalogNameValidator : AbstractValidator<string>
{
    public const int NameMaxLength = 100;

    public CatalogNameValidator()
    {
        RuleFor(n => n).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name is required.")
            .Must(n => n.Trim().Length <= NameMaxLength).WithMessage($"Name must be at most {NameMaxLength} characters.")
            // A name without one gives an empty slug.
            .Must(n => CatalogSlug.From(n).Length > 0).WithMessage("Name needs at least one Latin letter or digit.");
    }
}
