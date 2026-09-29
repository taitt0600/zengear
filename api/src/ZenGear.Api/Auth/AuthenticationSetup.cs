using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using ZenGear.Api.OpenApi;

namespace ZenGear.Api.Auth;

public static class AuthenticationSetup
{
    public const string TestingEnvironment = "Testing";

    /// <summary>
    /// Firebase ID token as JWT Bearer. Nothing is validated or fetched here: the options are
    /// checked when the host starts (<c>ValidateOnStart</c>, skipped during build-time OpenAPI
    /// generation) and the signing keys are fetched at the first request, so the build runs
    /// without configuration or network.
    /// Every endpoint requires a registered, ACTIVE user unless it is marked <c>[AllowAnonymous]</c>.
    /// </summary>
    public static IServiceCollection AddFirebaseJwtBearer(this IServiceCollection services, IHostEnvironment environment)
    {
        var firebaseOptions = services.AddOptions<FirebaseAuthOptions>()
            .BindConfiguration(FirebaseAuthOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => !o.UseEmulator || environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironment),
                "Firebase:UseEmulator is allowed only in the Development and Testing environments.");
        if (!BuildTimeDocumentGeneration.IsActive)
        {
            firebaseOptions.ValidateOnStart();
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FirebaseAuthOptions>>((options, firebaseOptions) =>
                ConfigureJwtBearer(options, firebaseOptions.Value));

        // Default ([Authorize]) and fallback (no attribute) both require a registered, ACTIVE user:
        // the claim is added by ActiveUserMiddleware. Changing only the fallback would let an
        // unregistered uid through every bare [Authorize].
        var registeredUser = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(ZenGearClaims.UserId)
            .Build();
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(registeredUser)
            .SetFallbackPolicy(registeredUser);
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AccountNotRegisteredResultHandler>();

        return services;
    }

    /// <summary>Between <c>UseAuthentication</c> and <c>UseAuthorization</c>.</summary>
    public static IApplicationBuilder UseActiveUser(this IApplicationBuilder app) =>
        app.UseMiddleware<ActiveUserMiddleware>();

    private static void ConfigureJwtBearer(JwtBearerOptions options, FirebaseAuthOptions firebase)
    {
        // Keep the JWT claim names: "sub" is the Firebase uid, "auth_time" marks a new sign-in (R-SEC-11).
        options.MapInboundClaims = false;

        var parameters = options.TokenValidationParameters;
        parameters.ValidIssuer = firebase.Issuer;
        parameters.ValidAudience = firebase.ProjectId;
        parameters.ValidateIssuer = true;
        parameters.ValidateAudience = true;
        parameters.ValidateLifetime = true;
        parameters.ClockSkew = TimeSpan.FromMinutes(1);
        parameters.NameClaimType = "sub";

        if (firebase.UseEmulator)
        {
            // The Auth Emulator issues unsigned tokens (alg "none") with the same issuer and audience.
            parameters.RequireSignedTokens = false;
            parameters.ValidateIssuerSigningKey = false;
            parameters.SignatureValidator = (token, _) => new JsonWebToken(token);
            return;
        }

        options.Authority = firebase.Issuer;
    }
}
