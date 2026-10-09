using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace ZenGear.Data.Common;

/// <summary>
/// Schema objects of api/db/ that the EF Core model does not hold. A composite foreign key whose target is a unique
/// index, not a key (R-DB-09: <c>(ward_id, province_id)</c> → <c>wards (id, province_id)</c>), is not an EF Core
/// relationship: EF Core would add an alternate key the specification does not have (docs/ARCHITECTURE.md, Data access).
/// </summary>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// <c>ALTER TABLE … ADD CONSTRAINT … FOREIGN KEY (…) REFERENCES … (…) ON DELETE …</c>, every identifier quoted.
    /// Drop it in <c>Down</c> with <see cref="DropCompositeForeignKey"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A blank identifier, no column, a different number of columns on each
    /// side, or a <see cref="CompositeForeignKeyOnDelete.SetNullColumns"/> column that is not a column of the key.</exception>
    public static OperationBuilder<SqlOperation> AddCompositeForeignKey(this MigrationBuilder migrationBuilder, string name,
        string table, string[] columns, string principalTable, string[] principalColumns, CompositeForeignKeyOnDelete onDelete)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalTable);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(principalColumns);
        ArgumentNullException.ThrowIfNull(onDelete);

        if (columns.Length == 0 || columns.Length != principalColumns.Length)
        {
            throw new ArgumentException(
                $"A foreign key has as many columns as the columns it references ({columns.Length} and {principalColumns.Length}), at least one.",
                nameof(principalColumns));
        }

        foreach (var column in columns.Concat(principalColumns))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(column);
        }

        if (onDelete.SetNullColumnNames.Except(columns, StringComparer.Ordinal).Any())
        {
            throw new ArgumentException("SET NULL (…) names only columns of the foreign key.", nameof(onDelete));
        }

        return migrationBuilder.Sql(
            $"ALTER TABLE {QuoteIdentifier(table)} ADD CONSTRAINT {QuoteIdentifier(name)} " +
            $"FOREIGN KEY ({QuoteIdentifiers(columns)}) REFERENCES {QuoteIdentifier(principalTable)} ({QuoteIdentifiers(principalColumns)}) " +
            $"{onDelete.ToSql()};");
    }

    /// <summary><c>ALTER TABLE … DROP CONSTRAINT …</c>: the <c>Down</c> of <see cref="AddCompositeForeignKey"/>.</summary>
    public static OperationBuilder<SqlOperation> DropCompositeForeignKey(this MigrationBuilder migrationBuilder, string name, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        return migrationBuilder.Sql($"ALTER TABLE {QuoteIdentifier(table)} DROP CONSTRAINT {QuoteIdentifier(name)};");
    }

    internal static string QuoteIdentifiers(IEnumerable<string> identifiers) => string.Join(", ", identifiers.Select(QuoteIdentifier));

    // PostgreSQL quoted identifier: a double quote inside it is doubled.
    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
