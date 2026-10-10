using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ZenGear.Data.Entities.Identity;

namespace ZenGear.Services.Identity.Access.AccessCache;

/// <summary>
/// Bumps the access cache versions after a change to users.status, staff_profiles, user_roles or
/// role_permissions is committed (R-SEC-07). Bumping only after the commit matters: bumped before, a
/// parallel request could still read the old rows and cache them under the new version.
/// One instance per DbContext (scoped). Changes made outside the change tracker are not seen (see <see cref="IAccessCache"/>).
/// </summary>
public sealed class AccessCacheInvalidationInterceptor(IAccessCache cache) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly HashSet<AccessBump> _pending = [];

    /// <summary>The versions a set of tracked changes affects.</summary>
    public static IEnumerable<AccessBump> CollectBumps(ChangeTracker changeTracker)
    {
        foreach (var entry in changeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case User user when entry.State == EntityState.Deleted
                    || (entry.State == EntityState.Modified
                        && (entry.Property(nameof(User.Status)).IsModified || entry.Property(nameof(User.FirebaseUid)).IsModified)):
                    yield return AccessBump.User(user.Id);
                    break;
                case StaffProfile profile when entry.State is EntityState.Added or EntityState.Deleted
                    || (entry.State == EntityState.Modified && entry.Property(nameof(StaffProfile.MustChangePassword)).IsModified):
                    yield return AccessBump.User(profile.UserId);
                    break;
                case UserRole userRole when entry.State is EntityState.Added or EntityState.Deleted:
                    yield return AccessBump.User(userRole.UserId);
                    break;
                case RolePermission rolePermission when entry.State is EntityState.Added or EntityState.Deleted:
                    yield return AccessBump.Role(rolePermission.RoleId);
                    break;
                case Role role when entry.State == EntityState.Deleted
                    || (entry.State == EntityState.Modified && entry.Property(nameof(Role.ScopingType)).IsModified):
                    yield return AccessBump.Role(role.Id);
                    break;
            }
        }
    }

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData);
        return ValueTask.FromResult(result);
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            FlushAsync().GetAwaiter().GetResult();
        }

        return result;
    }

    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            await FlushAsync();
        }

        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            _pending.Clear();
        }
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        FlushAsync().GetAwaiter().GetResult();

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) =>
        FlushAsync();

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => _pending.Clear();

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return Task.CompletedTask;
    }

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => _pending.Clear();

    public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return Task.CompletedTask;
    }

    private void Collect(DbContextEventData eventData)
    {
        if (eventData.Context is { } context)
        {
            _pending.UnionWith(CollectBumps(context.ChangeTracker));
        }
    }

    /// <summary>Bumps once: EF raises both TransactionCommitted and SavedChanges for its own transaction.</summary>
    private Task FlushAsync()
    {
        if (_pending.Count == 0)
        {
            return Task.CompletedTask;
        }

        var bumps = _pending.ToList();
        _pending.Clear();
        return cache.BumpAsync(bumps);
    }
}
