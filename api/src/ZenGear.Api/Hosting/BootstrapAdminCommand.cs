using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Options;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Api.Hosting;

/// <summary>
/// The one-off command <c>dotnet ZenGear.Api.dll bootstrap-admin --email &lt;email&gt; --name &lt;name&gt;</c> that creates the
/// first Admin of an environment (DEC-096). It runs instead of the web server: no request is served and no job starts.
/// The password reset link is written to the operator's terminal only, never to a log. A refusal is written with the
/// texts of the API's problems, in the default language (DEC-117 (3)): no request sets the culture here.
/// </summary>
public static class BootstrapAdminCommand
{
    public const string Name = "bootstrap-admin";

    public const int Succeeded = 0;
    public const int Refused = 1;
    public const int UsageError = 2;

    private const string Usage = "Usage: dotnet ZenGear.Api.dll bootstrap-admin --email <email> --name <name>";

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    /// <returns>The exit code: <see cref="Succeeded"/>, <see cref="Refused"/> or <see cref="UsageError"/>.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        CultureInfo.CurrentUICulture = LocalizationSetup.DefaultUiCulture;
        var texts = services.GetRequiredService<ProblemTexts>();
        if (ReadOption(args, "--email") is not { } email || ReadOption(args, "--name") is not { } name)
        {
            await output.WriteLineAsync(Usage);
            return UsageError;
        }

        // The host is never started, so the options checks that run at startup are run here.
        try
        {
            services.GetService<IStartupValidator>()?.Validate();
        }
        catch (OptionsValidationException ex)
        {
            await output.WriteLineAsync($"Configuration not valid: {string.Join(" ", ex.Failures)}");
            return Refused;
        }

        await using var scope = services.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<StaffBootstrapService>();
        try
        {
            var result = await bootstrap.BootstrapAdminAsync(email, name, cancellationToken);
            await output.WriteLineAsync($"Admin created: user {result.UserId}.");
            if (result.PasswordResetLink is null)
            {
                await output.WriteLineAsync(
                    "The password reset link could not be generated. Send one from the Firebase console (Authentication, the " +
                    "user's menu, Reset password) or use the forgot-password link of the web app with this email.");
                return Refused;
            }

            await output.WriteLineAsync("Open this link to set the password (it is a credential: do not share it):");
            await output.WriteLineAsync(result.PasswordResetLink);
            return Succeeded;
        }
        catch (ValidationException ex)
        {
            await output.WriteLineAsync(string.Join(" ", ex.Errors.Select(e => texts.ErrorDetail(e.ErrorCode, e.FormattedMessagePlaceholderValues))));
            await output.WriteLineAsync(Usage);
            return UsageError;
        }
        catch (BusinessRuleException ex)
        {
            await output.WriteLineAsync(texts.Detail(ex.ProblemType));
            return Refused;
        }
    }

    private static string? ReadOption(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[index + 1]
            : null;
    }
}
