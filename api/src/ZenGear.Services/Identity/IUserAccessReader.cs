using ZenGear.Services.Identity.Models;



namespace ZenGear.Services.Identity;

/// <summary>
/// The one lookup of the caller per request: status, sign-in, password flag and permissions,
/// through the Redis access cache (R-SEC-07).
/// </summary>
public interface IUserAccessReader
{
    /// <returns>Null when no user is linked to the Firebase uid.</returns>
    Task<UserAccess?> GetAsync(string firebaseUid, CancellationToken cancellationToken);
}
