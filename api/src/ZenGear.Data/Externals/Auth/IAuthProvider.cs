namespace ZenGear.Data.Externals.Auth;

/// <summary>
/// The accounts of Firebase Authentication, through the Firebase Admin SDK (ARCHITECTURE.md, rule 6). The business
/// rules built on them stay in the services; the default staff password never leaves the implementation.
/// </summary>
public interface IAuthProvider
{
    /// <summary>The account of <paramref name="email"/>; null when there is none.</summary>
    Task<AuthAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an account with the default staff password (<see cref="AuthProviderOptions.DefaultStaffPassword"/>).
    /// </summary>
    /// <returns>Null when <paramref name="email"/> already has an account.</returns>
    /// <exception cref="InvalidOperationException">The default staff password is not configured; nothing is created.</exception>
    Task<AuthAccount?> CreateWithDefaultStaffPasswordAsync(string email, CancellationToken cancellationToken);

    /// <summary>Creates an account without a password; its owner sets one through a reset link (DEC-096).</summary>
    /// <returns>Null when <paramref name="email"/> already has an account.</returns>
    Task<AuthAccount?> CreateWithoutPasswordAsync(string email, CancellationToken cancellationToken);

    /// <summary>Deletes an account; nothing happens when it no longer exists.</summary>
    Task DeleteAsync(string uid, CancellationToken cancellationToken);

    /// <summary>A link that lets the owner of <paramref name="email"/> set a password. It is a credential: never log it.</summary>
    Task<string> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken);

    /// <summary>Sets the password of the account <paramref name="uid"/> (DEC-094). Never log <paramref name="newPassword"/>.</summary>
    Task ChangePasswordAsync(string uid, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every refresh token of the account <paramref name="uid"/>: each device must sign in again once its ID
    /// token expires (DEC-094). ID tokens already issued stay valid until then. Nothing happens when the account no
    /// longer exists.
    /// </summary>
    Task RevokeSessionsAsync(string uid, CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="password"/> is exactly the default staff password, character for character (R-USR-11); false
    /// when none is configured. The comparison stays here so the default password never leaves the implementation.
    /// </summary>
    bool IsDefaultStaffPassword(string password);
}
