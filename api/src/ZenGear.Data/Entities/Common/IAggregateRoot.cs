namespace ZenGear.Data.Entities.Common;

/// <summary>
/// Marks an aggregate root. <see cref="Version"/> is mapped to the PostgreSQL system column
/// <c>xmin</c> and used as the optimistic concurrency token (docs/DATABASE.md, Concurrency).
/// </summary>
public interface IAggregateRoot
{
    uint Version { get; }
}
