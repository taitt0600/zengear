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
using ZenGear.Data.Externals.Redis;
using ZenGear.Services.Identity.Account;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The API in environment <c>Testing</c> against the test containers, with the Firebase Auth Emulator
/// token mode (project <c>demo-zengear</c>), the web origin allowed by CORS, a fake clock and the test-only
/// controllers of this assembly.
/// </summary>
public class ZenGearApiFactory(string environment = AuthenticationSetup.TestingEnvironment, string? redisConnectionString = null)
    : WebApplicationFactory<Program>
{
    public const string FirebaseProjectId = "demo-zengear";

    /// <summary>The origin of the web in development, the only one <c>Cors:AllowedOrigins</c> lists here.</summary>
    public const string WebOrigin = "http://localhost:3000";

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 29, 9, 30, 0, TimeSpan.Zero));

    /// <summary>Entries written through <see cref="SecurityLog"/>.</summary>
    public InMemoryLoggerProvider SecurityLogs { get; } = new();

    /// <summary>Firebase Authentication of this API instance.</summary>
    public FakeAuthProvider Auth { get; } = new();

    /// <summary>Log entries of the staff services (Serilog replaces the logger factory, so they are captured by type).</summary>
    public InMemoryLoggerProvider StaffLogs { get; } = new();

    /// <summary>Log entries of the Redis connection.</summary>
    public InMemoryLoggerProvider RedisLogs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:ZenGear", PostgresContainer.ConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", redisConnectionString ?? RedisContainer.ConnectionString);
        builder.UseSetting("Firebase:ProjectId", FirebaseProjectId);
        builder.UseSetting("Firebase:UseEmulator", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", WebOrigin);

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
            services.AddSingleton<ILogger<StaffStatusService>>(new Logger<StaffStatusService>(staffLogs));
            services.AddSingleton<ILogger<RedisConnection>>(
                new Logger<RedisConnection>(LoggerFactory.Create(logging => logging.AddProvider(RedisLogs))));
            services.AddControllers().AddApplicationPart(typeof(ZenGearApiFactory).Assembly);
        });
    }
}
