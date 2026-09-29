using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ZenGear.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ZenGearDbContext"/>. A missing connection string fails when the context
    /// is first used, not here, so the build-time OpenAPI generation runs without secrets.
    /// No retrying execution strategy: it conflicts with the user-initiated transaction of each use case.
    /// Interceptors registered as <see cref="IInterceptor"/> by the upper layers are added to every context.
    /// </summary>
    public static IServiceCollection AddZenGearData(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<ZenGearDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetServices<IInterceptor>()));

        return services;
    }
}
