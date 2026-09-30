using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ZenGear.Data.Common;

/// <summary>
/// Stores an enum as the value of its <see cref="JsonStringEnumMemberNameAttribute"/>, so the
/// database and the JSON both carry the value listed in docs/DATABASE.md (<c>UserStatus.Active</c> ↔ <c>ACTIVE</c>).
/// Applied to every enum property by <see cref="ZenGearDbContext"/>; every member must declare the attribute.
/// </summary>
public sealed class AttributeEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    private static readonly Mapping EnumMapping = BuildMapping();

    private static readonly FrozenDictionary<TEnum, string> ToStoredValues = EnumMapping.ByValue;

    private static readonly FrozenDictionary<string, TEnum> FromStoredValues = EnumMapping.ByStoredValue;

    private static readonly string? MappingError = EnumMapping.Error;

    public AttributeEnumConverter()
        : base(v => ToStored(v), v => FromStored(v))
    {
        if (MappingError is not null)
        {
            throw new InvalidOperationException(MappingError);
        }
    }

    /// <summary>
    /// Never throws: a type initializer that throws would surface as <see cref="TypeInitializationException"/>,
    /// so a member without the attribute, two members with the same value and two members with the same
    /// stored value are reported through <see cref="Mapping.Error"/> and thrown by the constructor.
    /// </summary>
    private static Mapping BuildMapping()
    {
        var names = typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (Value: (TEnum)f.GetValue(null)!, f.Name, Stored: f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name))
            .ToList();

        var enumName = typeof(TEnum).Name;
        var errors = new List<string>();

        var missing = names.Where(n => n.Stored is null).Select(n => n.Name).ToList();
        if (missing.Count > 0)
        {
            errors.Add($"Enum {enumName} members {string.Join(", ", missing)} have no [JsonStringEnumMemberName] stored value.");
        }

        errors.AddRange(names.GroupBy(n => n.Value)
            .Where(g => g.Count() > 1)
            .Select(g => $"Enum {enumName} members {string.Join(", ", g.Select(n => n.Name))} have the same value."));

        errors.AddRange(names.Where(n => n.Stored is not null)
            .GroupBy(n => n.Stored!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"Enum {enumName} members {string.Join(", ", g.Select(n => n.Name))} have the same stored value '{g.Key}'."));

        if (errors.Count > 0)
        {
            return new Mapping(FrozenDictionary<TEnum, string>.Empty, FrozenDictionary<string, TEnum>.Empty, string.Join(" ", errors));
        }

        return new Mapping(
            names.ToFrozenDictionary(n => n.Value, n => n.Stored!),
            names.ToFrozenDictionary(n => n.Stored!, n => n.Value, StringComparer.Ordinal),
            null);
    }

    private static string ToStored(TEnum value) =>
        ToStoredValues.TryGetValue(value, out var stored)
            ? stored
            : throw new InvalidOperationException($"Enum {typeof(TEnum).Name} has no stored value for {value}.");

    private static TEnum FromStored(string stored) =>
        FromStoredValues.TryGetValue(stored, out var value)
            ? value
            : throw new InvalidOperationException($"Database value '{stored}' is not a member of enum {typeof(TEnum).Name}.");

    private sealed record Mapping(
        FrozenDictionary<TEnum, string> ByValue,
        FrozenDictionary<string, TEnum> ByStoredValue,
        string? Error);
}
