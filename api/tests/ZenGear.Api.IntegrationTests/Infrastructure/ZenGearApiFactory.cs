using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ZenGear.Api.Extensions;
using ZenGear.Api.Logging;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Identity;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The API in environment <c>Testing</c> against the test containers, with the Firebase Auth Emulator
/// token mode (project <c>demo-zengear</c>), a fake clock and the test-only controllers of this assembly.
/// </summary>
public class ZenGearApiFactory(string environment = AuthenticationSetup.TestingEnvironment, string? redisConnectionString = null)
    : WebApplicationFactory<Program>
{
    public const string FirebaseProjectId = "demo-zengear";

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 29, 9, 30, 0, TimeSpan.Zero));

    /// <summary>Entries written through <see cref="SecurityLog"/>.</summary>
    public InMemoryLoggerProvider SecurityLogs { get; } = new();

    /// <summary>Firebase Authentication of this API instance.</summary>
    public FakeAuthProvider Auth { get; } = new();

    /// <summary>Log entries of the staff services (Serilog replaces the logger factory, so they are captured by type).</summary>
    public InMemoryLoggerProvider StaffLogs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:ZenGear", PostgresContainer.ConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", redisConnectionString ?? RedisContainer.ConnectionString);
        builder.UseSetting("Firebase:ProjectId", FirebaseProjectId);
        builder.UseSetting("Firebase:UseEmulator", "true");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<SecurityLog>();
            services.AddSingleton(new SecurityLog(LoggerFactory.Create(logging => logging.AddProvider(SecurityLogs))));
            services.RemoveAll<IAuthProvider>();
            services.AddSingleton<IAuthProvider>(Auth);
            var staffLogs = LoggerFactory.Create(logging => logging.AddProvider(StaffLogs));
            services.AddSingleton<ILogger<StaffService>>(new Logger<StaffService>(staffLogs));
            services.AddSingleton<ILogger<StaffBootstrapService>>(new Logger<StaffBootstrapService>(staffLogs));
            services.AddSingleton<ILogger<CurrentUserService>>(new Logger<CurrentUserService>(staffLogs));
            services.AddControllers().AddApplicationPart(typeof(ZenGearApiFactory).Assembly);
        });
    }
}
