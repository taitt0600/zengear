namespace ZenGear.Data.Common;

/// <summary>
/// The <c>ON DELETE</c> action of a composite foreign key written by
/// <see cref="MigrationBuilderExtensions.AddCompositeForeignKey"/>. A closed set: EF Core's <c>ReferentialAction</c>
/// has no <c>SET NULL (column)</c>, which <c>fk_product_images_product_skus_sku_id</c> needs (api/db/).
/// </summary>
public sealed class CompositeForeignKeyOnDelete
{
    private readonly string action;

    private CompositeForeignKeyOnDelete(string action, IReadOnlyList<string> setNullColumns)
    {
        this.action = action;
        SetNullColumnNames = setNullColumns;
    }

    /// <summary><c>ON DELETE RESTRICT</c>.</summary>
    public static CompositeForeignKeyOnDelete Restrict { get; } = new("RESTRICT", []);

    /// <summary><c>ON DELETE CASCADE</c>.</summary>
    public static CompositeForeignKeyOnDelete Cascade { get; } = new("CASCADE", []);

    /// <summary><c>ON DELETE SET NULL</c>: every column of the key.</summary>
    public static CompositeForeignKeyOnDelete SetNull { get; } = new("SET NULL", []);

    /// <summary>The columns <see cref="SetNullColumns"/> clears; empty for the other actions.</summary>
    public IReadOnlyList<string> SetNullColumnNames { get; }

    /// <summary><c>ON DELETE SET NULL (column, …)</c>: only these columns of the key, the others keep their value.</summary>
    public static CompositeForeignKeyOnDelete SetNullColumns(params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
        {
            throw new ArgumentException("SET NULL (…) names at least one column; use SetNull for every column.", nameof(columns));
        }

        foreach (var column in columns)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(column);
        }

        return new CompositeForeignKeyOnDelete("SET NULL", [.. columns]);
    }

    internal string ToSql() => SetNullColumnNames.Count == 0
        ? $"ON DELETE {action}"
        : $"ON DELETE {action} ({MigrationBuilderExtensions.QuoteIdentifiers(SetNullColumnNames)})";
}
