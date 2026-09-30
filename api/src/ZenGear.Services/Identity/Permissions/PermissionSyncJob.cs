using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// Runs <see cref="PermissionSyncService"/> once when the API starts (DEC-095). A failure is logged and the
/// API starts anyway with the grants already in the database.
/// </summary>
public sealed class PermissionSyncJob(IServiceScopeFactory scopes, ILogger<PermissionSyncJob> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PermissionSyncService>().SyncAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Permission sync failed; the API starts with the grants already in the database");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
