namespace ZenGear.Data.Entities.Geography;

/// <summary>
/// Country reference data (<c>countries</c>), loaded from the official list once per environment; the application only
/// reads it.
/// </summary>
public class Country
{
    protected Country()
    {
    }

    /// <summary>The id of the seed source, not generated.</summary>
    public int Id { get; private set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string CountryCode { get; private set; } = null!;

    /// <summary>ISO 3166-1 alpha-3.</summary>
    public string? CountryCode3 { get; private set; }

    /// <summary>ISO 3166-1 numeric.</summary>
    public string? CountryNumber { get; private set; }

    public string? CommonName { get; private set; }

    public string? FormalName { get; private set; }

    public string? CountryType { get; private set; }

    public string? CountrySubType { get; private set; }

    public string? Sovereignty { get; private set; }

    public string? Capital { get; private set; }

    /// <summary>ISO 4217.</summary>
    public string? CurrencyCode { get; private set; }

    public string? CurrencyName { get; private set; }

    public string? TelephoneCode { get; private set; }

    public string? InternetCountryCode { get; private set; }

    public string? FlagUrl { get; private set; }

    public int? SortOrder { get; private set; }

    public bool IsPublished { get; private set; }

    public bool IsDeleted { get; private set; }
}
