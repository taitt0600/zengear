namespace ZenGear.Data.Externals.Auth;

/// <summary>
/// Section <c>Firebase</c>, read by <see cref="FirebaseAuthProvider"/>. <c>ProjectId</c> and <c>UseEmulator</c> mean
/// the same as for the ID token validation of the API, which binds the same section.
/// </summary>
public sealed class AuthProviderOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Firebase's own minimum password length.</summary>
    public const int MinimumPasswordLength = 6;

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Talk to the Firebase Auth Emulator named by the environment variable <c>FIREBASE_AUTH_EMULATOR_HOST</c>
    /// (Development and Testing only, checked by the API at startup).
    /// </summary>
    public bool UseEmulator { get; set; }

    /// <summary>
    /// The password every new staff account gets (STF-01). Set by the environment variable
    /// <c>Firebase__DefaultStaffPassword</c> or the user-secrets, never in a settings file; never logged.
    /// </summary>
    public string? DefaultStaffPassword { get; set; }
}
