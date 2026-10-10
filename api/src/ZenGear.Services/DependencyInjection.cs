using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZenGear.Data;
using ZenGear.Data.Externals.Auth;
using ZenGear.Data.Externals.Redis;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Access;
using ZenGear.Services.Identity.Access.AccessCache;
using ZenGear.Services.Identity.Account;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.StaffAccounts;
using ZenGear.Services.Locations.BusinessLocations;

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

        services.AddOptions<AuthProviderOptions>().BindConfiguration(AuthProviderOptions.SectionName);
        services.AddSingleton<IAuthProvider, FirebaseAuthProvider>();
        services.AddValidatorsFromAssemblyContaining<StaffService>();

        services.AddScoped<IUserAccessReader, UserAccessReader>();
        services.AddScoped<SignInService>();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<StaffService>();
        services.AddScoped<StaffStatusService>();
        services.AddScoped<StaffBootstrapService>();
        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();
        services.AddScoped<PermissionSyncService>();
        services.AddScoped<BusinessLocationService>();
        services.AddScoped<BusinessLocationPublicService>();

        services.AddHealthChecks().AddDbContextCheck<ZenGearDbContext>();

        return services;
    }
}
