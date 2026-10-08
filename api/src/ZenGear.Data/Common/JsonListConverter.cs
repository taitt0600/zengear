using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ZenGear.Data.Common;

/// <summary>
/// Stores a list of immutable records as a JSON array in a <c>jsonb</c> column, with
/// <see cref="HourMinuteTimeJsonConverter.JsonbOptions"/>. Pair it with <see cref="Comparer"/> so a changed list is saved.
/// </summary>
public sealed class JsonListConverter<T>() : ValueConverter<IReadOnlyList<T>, string>(
    list => JsonSerializer.Serialize(list, HourMinuteTimeJsonConverter.JsonbOptions),
    json => JsonSerializer.Deserialize<List<T>>(json, HourMinuteTimeJsonConverter.JsonbOptions) ?? new List<T>())
{
    /// <summary>Compares the elements (records compare by value); the elements are immutable, so a copy of the list is a snapshot.</summary>
    public ValueComparer<IReadOnlyList<T>> Comparer { get; } = new(
        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());
}
