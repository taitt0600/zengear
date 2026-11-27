using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Audit.Enums;

namespace ZenGear.Services.Audit.AuditLogs;

/// <summary>
/// Writes the <c>audit_logs</c> rows of each <c>SaveChanges</c> in the transaction of the changes (R-AUD-01, DEC-086),
/// so they share its <c>transaction_id</c> and are rolled back with it. The rows are written after the save, when the
/// generated keys of the inserted rows are known; a save outside a transaction gets one from this interceptor, opened
/// before and committed after the rows. <c>ExecuteUpdate</c>, <c>ExecuteDelete</c>, raw SQL and the cascades of the
/// database never reach the change tracker, so they are not audited.
/// One instance per DbContext (scoped).
/// </summary>
public sealed class AuditLogInterceptor(AuditActor actor) : SaveChangesInterceptor
{
    private static readonly AttributeEnumConverter<AuditAction> ActionConverter = new();

    private List<AuditChange> _pending = [];

    private IDbContextTransaction? _ownTransaction;

    private DatabaseFacade? _ownTransactionDatabase;

    private bool _savepointsBefore;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (Collect(eventData) is { } database)
        {
            _ownTransaction = database.BeginTransaction();
            WithoutSavepoints(database);
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Collect(eventData) is { } database)
        {
            _ownTransaction = await database.BeginTransactionAsync(cancellationToken);
            WithoutSavepoints(database);
        }

        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var changes = TakePending();
        if (changes.Count == 0 || eventData.Context is not { } context)
        {
            return result;
        }

        try
        {
            context.Database.ExecuteSql(Insert(changes));
        }
        catch
        {
            Abandon();
            throw;
        }

        // A failed COMMIT has ended the transaction on the server: it is only disposed, and EF Core raises
        // TransactionFailed for it. The deferred triggers of user_roles (R-SEC-01, R-SEC-02) fail here.
        try
        {
            _ownTransaction?.Commit();
        }
        finally
        {
            EndOwnTransaction();
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var changes = TakePending();
        if (changes.Count == 0 || eventData.Context is not { } context)
        {
            return result;
        }

        try
        {
            await context.Database.ExecuteSqlAsync(Insert(changes), cancellationToken);
        }
        catch
        {
            await AbandonAsync();
            throw;
        }

        try
        {
            if (_ownTransaction is not null)
            {
                await _ownTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            await EndOwnTransactionAsync();
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Abandon();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default) => AbandonAsync();

    public override void SaveChangesCanceled(DbContextEventData eventData) => Abandon();

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData,
        CancellationToken cancellationToken = default) => AbandonAsync();

    /// <summary>The W3C trace id of the current activity, the TraceId the application log writes; null outside one.</summary>
    private static Guid? CorrelationId() =>
        Activity.Current is { } activity && activity.TraceId != default
            ? Guid.ParseExact(activity.TraceId.ToHexString(), "N")
            : null;

    /// <summary>Collects the changes; returns the database facade when a transaction must be opened for them.</summary>
    private DatabaseFacade? Collect(DbContextEventData eventData)
    {
        if (eventData.Context is not { } context)
        {
            _pending = [];
            return null;
        }

        _pending = AuditChange.Collect(context.ChangeTracker);
        return _pending.Count > 0 && context.Database.CurrentTransaction is null ? context.Database : null;
    }

    private List<AuditChange> TakePending()
    {
        var changes = _pending;
        _pending = [];
        return changes;
    }

    /// <summary>One statement for every row of the save, whatever their number.</summary>
    private FormattableString Insert(List<AuditChange> changes)
    {
        var rows = changes.Select(change => change.ToRow()).ToList();
        var tables = rows.Select(row => row.TableName).ToArray();
        var keys = rows.Select(row => row.RecordKey).ToArray();
        var actions = rows.Select(row => (string)ActionConverter.ConvertToProvider(row.Action)!).ToArray();
        var oldValues = rows.Select(row => row.OldValues).ToArray();
        var newValues = rows.Select(row => row.NewValues).ToArray();
        var correlationId = CorrelationId();
        var changedBy = actor.UserId;

        return $"""
            INSERT INTO audit_logs (table_name, record_key, action, old_values, new_values, correlation_id, changed_by)
            SELECT r.table_name, r.record_key::jsonb, r.action, r.old_values::jsonb, r.new_values::jsonb,
                   {correlationId}::uuid, {changedBy}::uuid
            FROM unnest({tables}::varchar[], {keys}::text[], {actions}::varchar[], {oldValues}::text[], {newValues}::text[])
                 AS r(table_name, record_key, action, old_values, new_values)
            """;
    }

    /// <summary>
    /// The save runs in this transaction as it would in the one EF Core opens itself: without the savepoint EF Core adds
    /// inside an existing transaction. A failure rolls the whole transaction back anyway, and the rows then carry the
    /// xid of <c>transaction_id</c> in <c>xmin</c>, not the xid of a subtransaction.
    /// </summary>
    private void WithoutSavepoints(DatabaseFacade database)
    {
        _ownTransactionDatabase = database;
        _savepointsBefore = database.AutoSavepointsEnabled;
        database.AutoSavepointsEnabled = false;
    }

    /// <summary>
    /// After a failed save or audit insert: an explicit rollback, not only a dispose, so EF Core raises
    /// <c>TransactionRolledBack</c> and the other interceptors (the access cache bumps of R-SEC-07) drop what they kept
    /// for this transaction. Skipped on a connection that is no longer open, where the rollback would throw and hide the
    /// error of the save; the state is reset whatever happens.
    /// </summary>
    private void Abandon()
    {
        _pending = [];
        try
        {
            if (CanRollBack())
            {
                _ownTransaction?.Rollback();
            }
        }
        finally
        {
            EndOwnTransaction();
        }
    }

    private async Task AbandonAsync()
    {
        _pending = [];
        try
        {
            if (CanRollBack() && _ownTransaction is not null)
            {
                await _ownTransaction.RollbackAsync();
            }
        }
        finally
        {
            await EndOwnTransactionAsync();
        }
    }

    private bool CanRollBack() =>
        _ownTransactionDatabase?.GetDbConnection().State == ConnectionState.Open;

    private void EndOwnTransaction()
    {
        _ownTransaction?.Dispose();
        _ownTransaction = null;
        RestoreSavepoints();
    }

    private async Task EndOwnTransactionAsync()
    {
        if (_ownTransaction is not null)
        {
            await _ownTransaction.DisposeAsync();
            _ownTransaction = null;
        }

        RestoreSavepoints();
    }

    /// <summary>The later transactions of the context, the ones a use case opens, keep the savepoints of EF Core.</summary>
    private void RestoreSavepoints()
    {
        if (_ownTransactionDatabase is not null)
        {
            _ownTransactionDatabase.AutoSavepointsEnabled = _savepointsBefore;
            _ownTransactionDatabase = null;
        }
    }
}
