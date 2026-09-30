using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZenGear.Data;
using ZenGear.Data.Externals.Redis;
using ZenGear.Services.Common;
using ZenGear.Services.Identity;
using ZenGear.Services.Identity.AccessCache;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the business logic layer and, through it, the data access layer.
    /// <c>dotnet ef</c> connects with <c>ConnectionStrings:ZenGearMigrations</c> (schema owner),
    /// the running application with <c>ConnectionStrings:ZenGear</c> (api/CLAUDE.md, DEC-089).
    /// Redis (<c>ConnectionStrings:Redis</c>) is connected on first use, never here.
    /// The jobs in <c>Jobs/</c> are registered by the host.
    /// </summary>
    public static IServiceCollection AddZenGearServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(EF.IsDesignTime ? "ZenGearMigrations" : "ZenGear");
        services.AddZenGearData(connectionString);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, Clock>();

        services.AddOptions<RedisOptions>().BindConfiguration(RedisOptions.SectionName);
        services.AddSingleton<RedisConnection>();
        services.AddSingleton<PendingAccessBumps>();
        services.AddSingleton<RedisAccessCache>();
        services.AddSingleton<IAccessCache>(sp => sp.GetRequiredService<RedisAccessCache>());
        services.AddScoped<IInterceptor, AccessCacheInvalidationInterceptor>();

        services.AddScoped<IUserAccessReader, UserAccessReader>();
        services.AddScoped<SignInService>();
        services.AddScoped<CurrentUserService>();
        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();
        services.AddScoped<PermissionSyncService>();

        services.AddHealthChecks().AddDbContextCheck<ZenGearDbContext>();

        return services;
    }
}
