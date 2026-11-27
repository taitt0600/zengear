using System.Collections;
using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using ZenGear.Data.Entities.Audit.Enums;

namespace ZenGear.Services.Audit.AuditLogs;

/// <summary>
/// One tracked change that R-AUD-01 audits. An UPDATE or a DELETE is read before the save, while the original values
/// are still known; an INSERT after it, once the database has filled the generated key and the defaults.
/// </summary>
public sealed class AuditChange
{
    /// <summary>The tables that keep a history of their own, and <c>audit_logs</c> itself (R-AUD-01).</summary>
    public static readonly FrozenSet<string> UnauditedTables = new[]
    {
        "daily_sequences", "orders", "stock_balances", "cart_items", "notifications", "status_history", "audit_logs",
    }.ToFrozenSet(StringComparer.Ordinal);

    private const string UsersTable = "users";

    private const string LastLoginAtColumn = "last_login_at";

    private readonly AuditRow? _row;

    private readonly EntityEntry? _inserted;

    private readonly string _table;

    private AuditChange(AuditRow row)
    {
        _row = row;
        _table = row.TableName;
    }

    private AuditChange(EntityEntry inserted, string table)
    {
        _inserted = inserted;
        _table = table;
    }

    /// <summary>The audited changes the change tracker holds, in its order.</summary>
    public static List<AuditChange> Collect(ChangeTracker changeTracker)
    {
        var changes = new List<AuditChange>();
        foreach (var entry in changeTracker.Entries())
        {
            var table = entry.Metadata.GetTableName();
            if (table is null || UnauditedTables.Contains(table))
            {
                continue;
            }

            var change = entry.State switch
            {
                EntityState.Added => new AuditChange(entry, table),
                EntityState.Modified => Updated(entry, table),
                EntityState.Deleted => new AuditChange(new AuditRow(table, Key(entry), AuditAction.Delete,
                    Values(entry, Columns(entry), original: true), null)),
                _ => null,
            };
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    /// <summary>The row to write; for an INSERT, call it only after the save.</summary>
    public AuditRow ToRow() => _inserted is { } entry
        ? new AuditRow(_table, Key(entry), AuditAction.Insert, null, Values(entry, Columns(entry), original: false))
        : _row ?? throw new InvalidOperationException("An audit change holds either a row or an inserted entry.");

    /// <summary>
    /// Only the columns whose value differs: a property marked modified with the same value (an <c>Update</c> of the whole
    /// entity) is not a change, and an UPDATE with nothing left, or with only <c>users.last_login_at</c> left, is not audited.
    /// </summary>
    private static AuditChange? Updated(EntityEntry entry, string table)
    {
        var changed = Columns(entry).Where(property => IsChanged(entry.Property(property.Name))).ToList();
        if (changed.Count == 0 || (table == UsersTable && changed is [var only] && only.GetColumnName() == LastLoginAtColumn))
        {
            return null;
        }

        return new AuditChange(new AuditRow(table, Key(entry), AuditAction.Update,
            Values(entry, changed, original: true), Values(entry, changed, original: false)));
    }

    private static bool IsChanged(PropertyEntry property) =>
        property.IsModified && !property.Metadata.GetValueComparer().Equals(property.OriginalValue, property.CurrentValue);

    /// <summary>The columns of the row: <c>xmin</c>, the concurrency token, is a system column, not data.</summary>
    private static IEnumerable<IProperty> Columns(EntityEntry entry) =>
        entry.Metadata.GetProperties().Where(property =>
            !(property.IsConcurrencyToken && property.ValueGenerated == ValueGenerated.OnAddOrUpdate));

    private static string Key(EntityEntry entry) =>
        Values(entry, entry.Metadata.FindPrimaryKey()!.Properties, original: false);

    private static string Values(EntityEntry entry, IEnumerable<IProperty> properties, bool original)
    {
        var values = new JsonObject();
        foreach (var property in properties)
        {
            var value = entry.Property(property.Name);
            values[property.GetColumnName()] = ToJson(property, original ? value.OriginalValue : value.CurrentValue);
        }

        return values.ToJsonString();
    }

    /// <summary>
    /// The value as the column stores it (an enum as its stored value, a JSON column as JSON), so a row of
    /// <c>audit_logs</c> reads like the table it describes.
    /// </summary>
    private static JsonNode? ToJson(IProperty property, object? value)
    {
        var converter = property.GetTypeMapping().Converter;
        var stored = value is null || converter is null ? value : converter.ConvertToProvider(value);
        return stored switch
        {
            null => null,
            string json when property.GetColumnType() == "jsonb" => JsonNode.Parse(json),
            string or bool or decimal or Guid or DateTimeOffset or DateTime or DateOnly or TimeOnly or TimeSpan
                or IEnumerable => JsonSerializer.SerializeToNode(stored, stored.GetType()),
            _ when stored.GetType().IsPrimitive => JsonSerializer.SerializeToNode(stored, stored.GetType()),
            _ => JsonValue.Create(stored.ToString()),
        };
    }
}
