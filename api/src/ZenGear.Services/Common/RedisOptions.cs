namespace ZenGear.Services.Common;

/// <summary>
/// Section <c>Redis</c>. The connection string is <c>ConnectionStrings:Redis</c> (user-secrets locally).
/// Short timeouts: a slow or missing Redis falls back to PostgreSQL instead of holding the request.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>Timeout of every Redis command.</summary>
    public int CommandTimeoutMs { get; set; } = 250;

    /// <summary>Timeout of one connection attempt; the client keeps reconnecting in the background.</summary>
    public int ConnectTimeoutMs { get; set; } = 1000;
}
