namespace ZenGear.Services.Identity.Access.AccessCache;

/// <summary>Versions under which entries are read and written: the global epoch and the entry's own version.</summary>
public readonly record struct AccessVersion(long Epoch, long Version);
