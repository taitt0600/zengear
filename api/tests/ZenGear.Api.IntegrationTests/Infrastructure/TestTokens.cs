using System.Net.Http.Headers;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>Unsigned ID tokens (alg "none") shaped like the ones of the Firebase Auth Emulator.</summary>
public static class TestTokens
{
    /// <param name="authTime">Sign-in time (<c>auth_time</c>); defaults to the issue time.</param>
    public static string EmulatorToken(string uid, DateTime? expires = null, DateTimeOffset? authTime = null,
        string projectId = ZenGearApiFactory.FirebaseProjectId)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);
        var issuedAt = expiresAt.AddHours(-1);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = $"https://securetoken.google.com/{projectId}",
            Audience = projectId,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = uid,
                ["auth_time"] = authTime?.ToUnixTimeSeconds() ?? EpochTime.GetIntDate(issuedAt),
            },
        });
    }

    public static HttpRequestMessage Get(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
