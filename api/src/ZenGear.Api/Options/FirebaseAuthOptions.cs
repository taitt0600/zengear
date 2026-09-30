using System.ComponentModel.DataAnnotations;

namespace ZenGear.Api.Options;

/// <summary>Configuration section <c>Firebase</c>.</summary>
public sealed class FirebaseAuthOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Firebase project id: the audience of ID tokens and the suffix of their issuer.</summary>
    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Accept the unsigned ID tokens of the Firebase Auth Emulator. Allowed only in Development and Testing.
    /// </summary>
    public bool UseEmulator { get; set; }

    public string Issuer => $"https://securetoken.google.com/{ProjectId}";
}
