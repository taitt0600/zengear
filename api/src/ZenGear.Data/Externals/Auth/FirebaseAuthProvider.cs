using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace ZenGear.Data.Externals.Auth;

/// <summary>
/// <see cref="IAuthProvider"/> over the Firebase Admin SDK. The Firebase app is created on first use, never when the
/// service is registered, so the build-time OpenAPI generation runs without credentials.
/// Credentials: the Auth Emulator (<c>FIREBASE_AUTH_EMULATOR_HOST</c>) when <see cref="AuthProviderOptions.UseEmulator"/>,
/// otherwise Application Default Credentials (<c>GOOGLE_APPLICATION_CREDENTIALS</c>).
/// </summary>
public sealed class FirebaseAuthProvider : IAuthProvider
{
    public const string EmulatorHostVariable = "FIREBASE_AUTH_EMULATOR_HOST";

    private const string AppName = "zengear";
    private static readonly Lock AppLock = new();

    private readonly AuthProviderOptions _options;
    private readonly Lazy<FirebaseAuth> _auth;

    public FirebaseAuthProvider(IOptions<AuthProviderOptions> options)
    {
        _options = options.Value;
        _auth = new Lazy<FirebaseAuth>(() => FirebaseAuth.GetAuth(GetOrCreateApp(_options)));
    }

    public async Task<AuthAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _auth.Value.GetUserByEmailAsync(email, cancellationToken);
            return new AuthAccount(user.Uid, user.Email);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            return null;
        }
    }

    public Task<AuthAccount?> CreateWithDefaultStaffPasswordAsync(string email, CancellationToken cancellationToken)
    {
        var password = DefaultStaffPassword(_options);
        return CreateAsync(new UserRecordArgs { Email = email, Password = password }, cancellationToken);
    }

    public Task<AuthAccount?> CreateWithoutPasswordAsync(string email, CancellationToken cancellationToken) =>
        CreateAsync(new UserRecordArgs { Email = email }, cancellationToken);

    public async Task DeleteAsync(string uid, CancellationToken cancellationToken)
    {
        try
        {
            await _auth.Value.DeleteUserAsync(uid, cancellationToken);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            // Already gone: the compensation has nothing left to do.
        }
    }

    public Task<string> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken) =>
        _auth.Value.GeneratePasswordResetLinkAsync(email, null, cancellationToken);

    /// <summary>The configured default staff password, checked before any call so nothing is created without it.</summary>
    /// <exception cref="InvalidOperationException">Missing or shorter than Firebase accepts; the message names the key only.</exception>
    public static string DefaultStaffPassword(AuthProviderOptions options) =>
        options.DefaultStaffPassword is { Length: >= AuthProviderOptions.MinimumPasswordLength } password
            ? password
            : throw new InvalidOperationException(
                $"Firebase:DefaultStaffPassword is not configured or is shorter than {AuthProviderOptions.MinimumPasswordLength} characters.");

    private async Task<AuthAccount?> CreateAsync(UserRecordArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _auth.Value.CreateUserAsync(args, cancellationToken);
            return new AuthAccount(user.Uid, user.Email);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
        {
            return null;
        }
    }

    private static FirebaseApp GetOrCreateApp(AuthProviderOptions options)
    {
        lock (AppLock)
        {
            return FirebaseApp.GetInstance(AppName) ?? FirebaseApp.Create(AppOptionsFor(options), AppName);
        }
    }

    /// <summary>
    /// The emulator is used only when asked for, and asking for it without the emulator host fails: a host that is not
    /// meant to use the emulator never sends accounts to one, and one that is never reaches the real project.
    /// </summary>
    private static AppOptions AppOptionsFor(AuthProviderOptions options)
    {
        var emulatorHost = Environment.GetEnvironmentVariable(EmulatorHostVariable);
        if (options.UseEmulator != !string.IsNullOrEmpty(emulatorHost))
        {
            throw new InvalidOperationException(options.UseEmulator
                ? $"Firebase:UseEmulator is true but {EmulatorHostVariable} is not set."
                : $"{EmulatorHostVariable} is set but Firebase:UseEmulator is false.");
        }

        return new AppOptions
        {
            ProjectId = string.IsNullOrEmpty(options.ProjectId) ? null : options.ProjectId,
            // The SDK replaces the credential with the emulator's "owner" token; one is still required.
            Credential = options.UseEmulator ? GoogleCredential.FromAccessToken("owner") : GoogleCredential.GetApplicationDefault(),
        };
    }
}
