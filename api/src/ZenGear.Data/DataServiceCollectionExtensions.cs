using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ZenGear.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ZenGearDbContext"/>. A missing connection string fails when the context
    /// is first used, not here, so the build-time OpenAPI generation runs without secrets.
    /// No retrying execution strategy: it conflicts with the user-initiated transaction of each use case.
    /// </summary>
    public static IServiceCollection AddZenGearData(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<ZenGearDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        return services;
    }
}
