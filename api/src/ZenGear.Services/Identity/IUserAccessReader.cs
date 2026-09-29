namespace ZenGear.Services.Identity;

/// <summary>
/// The one lookup of the caller per request. The Redis cache of user status and permissions
/// (R-SEC-07) belongs here, added with AUT-04.
/// </summary>
public interface IUserAccessReader
{
    /// <returns>Null when no user is linked to the Firebase uid.</returns>
    Task<UserAccess?> GetAsync(string firebaseUid, CancellationToken cancellationToken);
}
