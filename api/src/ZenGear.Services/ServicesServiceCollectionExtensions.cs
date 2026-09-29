using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZenGear.Data;
using ZenGear.Services.Common;

namespace ZenGear.Services;

public static class ServicesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the business logic layer and, through it, the data access layer.
    /// <c>dotnet ef</c> connects with <c>ConnectionStrings:ZenGearMigrations</c> (schema owner),
    /// the running application with <c>ConnectionStrings:ZenGear</c> (api/CLAUDE.md, DEC-089).
    /// </summary>
    public static IServiceCollection AddZenGearServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(EF.IsDesignTime ? "ZenGearMigrations" : "ZenGear");
        services.AddZenGearData(connectionString);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, Clock>();

        services.AddHealthChecks().AddDbContextCheck<ZenGearDbContext>();

        return services;
    }
}
