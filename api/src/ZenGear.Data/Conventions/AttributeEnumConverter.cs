using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ZenGear.Data.Conventions;

/// <summary>
/// Stores an enum as the value of its <see cref="JsonStringEnumMemberNameAttribute"/>, so the
/// database and the JSON both carry the value listed in docs/DATABASE.md (<c>UserStatus.Active</c> ↔ <c>ACTIVE</c>).
/// Applied to every enum property by <see cref="ZenGearDbContext"/>; every member must declare the attribute.
/// </summary>
public sealed class AttributeEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> ToStoredValues;

    private static readonly FrozenDictionary<string, TEnum> FromStoredValues;

    private static readonly string? MappingError;

    static AttributeEnumConverter()
    {
        var names = typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (Value: (TEnum)f.GetValue(null)!, f.Name, Stored: f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name))
            .ToList();

        var missing = names.Where(n => n.Stored is null).Select(n => n.Name).ToList();
        MappingError = missing.Count == 0
            ? null
            : $"Enum {typeof(TEnum).Name} members {string.Join(", ", missing)} have no [JsonStringEnumMemberName] stored value.";

        var mapped = names.Where(n => n.Stored is not null).ToList();
        ToStoredValues = mapped.ToFrozenDictionary(n => n.Value, n => n.Stored!);
        FromStoredValues = mapped.ToFrozenDictionary(n => n.Stored!, n => n.Value, StringComparer.Ordinal);
    }

    public AttributeEnumConverter()
        : base(v => ToStored(v), v => FromStored(v))
    {
        if (MappingError is not null)
        {
            throw new InvalidOperationException(MappingError);
        }
    }

    private static string ToStored(TEnum value) =>
        ToStoredValues.TryGetValue(value, out var stored)
            ? stored
            : throw new InvalidOperationException($"Enum {typeof(TEnum).Name} has no stored value for {value}.");

    private static TEnum FromStored(string stored) =>
        FromStoredValues.TryGetValue(stored, out var value)
            ? value
            : throw new InvalidOperationException($"Database value '{stored}' is not a member of enum {typeof(TEnum).Name}.");
}
