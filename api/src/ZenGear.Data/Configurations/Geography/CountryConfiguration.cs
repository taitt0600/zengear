using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZenGear.Data.Entities.Geography;

namespace ZenGear.Data.Configurations.Geography;

/// <summary><c>countries</c> as in api/db/zengear_02_schema_phase1_store.sql.</summary>
internal sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries");

        builder.HasKey(c => c.Id).HasName("pk_countries");
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CountryCode).HasMaxLength(2);
        builder.Property(c => c.CountryCode3).HasMaxLength(3);
        builder.Property(c => c.CountryNumber).HasMaxLength(3);
        builder.Property(c => c.CommonName).HasColumnType("varchar");
        builder.Property(c => c.FormalName).HasColumnType("varchar");
        builder.Property(c => c.CountryType).HasColumnType("varchar");
        builder.Property(c => c.CountrySubType).HasColumnType("varchar");
        builder.Property(c => c.Sovereignty).HasColumnType("varchar");
        builder.Property(c => c.Capital).HasColumnType("varchar");
        builder.Property(c => c.CurrencyCode).HasMaxLength(3);
        builder.Property(c => c.CurrencyName).HasColumnType("varchar");
        builder.Property(c => c.TelephoneCode).HasMaxLength(10);
        builder.Property(c => c.InternetCountryCode).HasMaxLength(10);
        builder.Property(c => c.FlagUrl).HasColumnType("text");
        builder.Property(c => c.IsPublished).HasDefaultValue(true);
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(c => c.CountryCode).IsUnique().HasDatabaseName("ix_countries_country_code");
        builder.HasIndex(c => c.CountryCode3).IsUnique().HasDatabaseName("ix_countries_country_code3");
    }
}
