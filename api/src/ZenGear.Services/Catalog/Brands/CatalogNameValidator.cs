using FluentValidation;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>
/// The name of a catalog record whose slug comes from it (brand, series): set on a request's name with
/// <c>SetValidator</c>, so the error stays on that field. Another record already holding the name is a 409 of the service.
/// Internal so that the assembly scan of the validators does not register it as the <c>IValidator&lt;string&gt;</c> of
/// every string. Each check has its own code (DEC-117 (2)); the API writes its text.
/// </summary>
internal sealed class CatalogNameValidator : AbstractValidator<string>
{
    public const int NameMaxLength = 100;

    public const string NameRequired = "NAME_REQUIRED";

    public const string NameTooLong = "NAME_TOO_LONG";

    public const string NameWithoutLetterOrDigit = "NAME_WITHOUT_LETTER_OR_DIGIT";

    public CatalogNameValidator()
    {
        RuleFor(n => n).Cascade(CascadeMode.Stop)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithErrorCode(NameRequired)
            .Must(FitsNameMaxLength).WithErrorCode(NameTooLong)
            // A name without one gives an empty slug.
            .Must(n => CatalogSlug.From(n).Length > 0).WithErrorCode(NameWithoutLetterOrDigit);
    }

    /// <summary>Also gives the limit to the text of the error (<c>{MaxLength}</c>).</summary>
    private static bool FitsNameMaxLength(string root, string name, ValidationContext<string> context)
    {
        context.MessageFormatter.AppendArgument("MaxLength", NameMaxLength);
        return name.Trim().Length <= NameMaxLength;
    }
}
