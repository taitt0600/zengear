namespace ZenGear.Data.Externals.Auth;

/// <summary>An account of the authentication provider (Firebase Authentication).</summary>
/// <param name="Uid">Firebase uid, stored in <c>users.firebase_uid</c>.</param>
public sealed record AuthAccount(string Uid, string Email);
