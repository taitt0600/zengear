using System.Collections.Concurrent;
using ZenGear.Data.Externals.Auth;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>Firebase Authentication in memory: the accounts by email, and hooks to make a test fail a step.</summary>
public sealed class FakeAuthProvider : IAuthProvider
{
    private readonly ConcurrentDictionary<string, FakeAuthAccount> _accounts = new(StringComparer.Ordinal);
    private int _nextUid;
    private TaskCompletionSource _deleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs after an account is created, before it is returned (e.g. to cancel the request).</summary>
    public Action<AuthAccount>? AfterCreate { get; set; }

    public bool FailPasswordResetLink { get; set; }

    public bool FailChangePassword { get; set; }

    public bool FailRevoke { get; set; }

    public IReadOnlyCollection<FakeAuthAccount> Accounts => [.. _accounts.Values];

    public List<string> Deleted { get; } = [];

    /// <summary>Uids whose sessions were revoked, in call order.</summary>
    public List<string> Revoked { get; } = [];

    public int ChangePasswordCalls => _changePasswordCalls;

    private int _changePasswordCalls;

    public void Reset()
    {
        _accounts.Clear();
        Deleted.Clear();
        Revoked.Clear();
        _changePasswordCalls = 0;
        AfterCreate = null;
        FailPasswordResetLink = false;
        FailChangePassword = false;
        FailRevoke = false;
        _deleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Completes at the first <see cref="DeleteAsync"/> (a request finishing after its client gave up).</summary>
    public Task WhenDeletedAsync() => _deleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

    /// <summary>An account registered in Firebase before ZenGear knows it.</summary>
    public FakeAuthAccount Add(string email, string password = "own-password")
    {
        var account = new FakeAuthAccount(NewUid(), email, password);
        _accounts[email] = account;
        return account;
    }

    /// <summary>An account with a chosen uid, for a user row inserted by <see cref="TestUsers"/>.</summary>
    public FakeAuthAccount AddWithUid(string uid, string email, string password)
    {
        var account = new FakeAuthAccount(uid, email, password);
        _accounts[email] = account;
        return account;
    }

    public FakeAuthAccount? Find(string email) => _accounts.GetValueOrDefault(email);

    public Task<AuthAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.TryGetValue(email, out var a) ? new AuthAccount(a.Uid, a.Email) : null);

    public Task<AuthAccount?> CreateWithDefaultStaffPasswordAsync(string email, CancellationToken cancellationToken) =>
        CreateAsync(email, DefaultPassword, cancellationToken);

    public Task<AuthAccount?> CreateWithoutPasswordAsync(string email, CancellationToken cancellationToken) =>
        CreateAsync(email, password: null, cancellationToken);

    public Task DeleteAsync(string uid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in _accounts.Where(e => e.Value.Uid == uid))
        {
            _accounts.TryRemove(entry.Key, out _);
        }

        lock (Deleted)
        {
            Deleted.Add(uid);
        }

        _deleted.TrySetResult();
        return Task.CompletedTask;
    }

    public Task<string> GeneratePasswordResetLinkAsync(string email, CancellationToken cancellationToken) =>
        FailPasswordResetLink
            ? throw new InvalidOperationException($"reset link failed for {email}")
            : Task.FromResult($"http://localhost:9099/emulator/action?mode=resetPassword&oobCode=code-{_accounts[email].Uid}");

    public Task ChangePasswordAsync(string uid, string newPassword, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _changePasswordCalls);
        if (FailChangePassword)
        {
            throw new InvalidOperationException($"password change failed for {uid}");
        }

        var entry = _accounts.Single(e => e.Value.Uid == uid);
        _accounts[entry.Key] = entry.Value with { Password = newPassword };
        return Task.CompletedTask;
    }

    public Task RevokeSessionsAsync(string uid, CancellationToken cancellationToken)
    {
        if (FailRevoke)
        {
            throw new InvalidOperationException($"revoke failed for {uid}");
        }

        lock (Revoked)
        {
            Revoked.Add(uid);
        }

        return Task.CompletedTask;
    }

    public bool IsDefaultStaffPassword(string password) => string.Equals(password, DefaultPassword, StringComparison.Ordinal);

    /// <summary>What the fake stores for "the default staff password".</summary>
    public const string DefaultPassword = "<default staff password>";

    private async Task<AuthAccount?> CreateAsync(string email, string? password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Lets parallel requests interleave like real network calls.
        await Task.Yield();
        var account = new FakeAuthAccount(NewUid(), email, password);
        if (!_accounts.TryAdd(email, account))
        {
            return null;
        }

        var created = new AuthAccount(account.Uid, email);
        AfterCreate?.Invoke(created);
        // Like the SDK: a call cancelled while it runs throws, although Firebase has created the account.
        cancellationToken.ThrowIfCancellationRequested();
        return created;
    }

    private string NewUid() => $"fake-uid-{Interlocked.Increment(ref _nextUid)}";
}
